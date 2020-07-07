using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Dynamics.CRM;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.OData;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Newtonsoft.Json.Linq;

namespace CrmOdataClient
{
    class Program
    {
        private static string _token;
        static void Main(string[] args)
        {
            _token = GetToken("365d020a-b2c1-412e-b9f8-b78c36c61501", "Blah", "https://login.microsoftonline.com/83f8ad7c-2f65-4302-9950-b0e6b2b72fbf/oauth2/v2.0/authorize").GetAwaiter().GetResult();
            var context = new Microsoft.Dynamics.CRM.System(new Uri("https://{crmUrl}/"));

            context.SendingRequest2 += ContextOnSendingRequest2;

            var contactId = Guid.Parse("99db51a2-c34e-e111-bb8d-00155d03a715");

            var dataServiceCollection =
                new DataServiceCollection<contact>(context.contacts.Where(x => x.contactid == contactId))
                {
                    [0] = {firstname = "Portal1"}
                };
            context.SaveChanges(SaveChangesOptions.PostOnlySetProperties);


            //CleanupMetaDataFile();
        }

        private static async Task<string> GetToken(string clientId, string clientSecret, string url)
        {
            var authenticationContext = new AuthenticationContext("https://login.microsoftonline.com/83f8ad7c-2f65-4302-9950-b0e6b2b72fbf");
            var result = await authenticationContext.AcquireTokenAsync("https://mytraining22.api.crm11.dynamics.com",
                new ClientCredential(clientId, clientSecret));

            return result.AccessToken;
        }

        private static void ContextOnSendingRequest2(object? sender, SendingRequest2EventArgs e)
        {
            e.RequestMessage.SetHeader("Authorization", $"Bearer {_token}");
        }

        private static DataServiceClientRequestMessage OnMessageCreating(DataServiceClientRequestMessageArgs args)
        {
            var message = new HttpClientRequestMessage(args.ActualMethod) { Url = args.RequestUri, Method = args.Method, };
            foreach (var header in args.Headers)
            {
                message.SetHeader(header.Key, header.Value);
            }

            return message;
        }

        private static void CleanupMetaDataFile()
        {
            var interestedProxyTypes = new Hashtable { { "contact", "contact" }, { "account", "account" } };

            var fileStream = File.OpenRead("metadata.xml");
            var edmModel = CsdlReader.Parse(new XmlTextReader(fileStream));

            var customEdmModel = new CustomEdmModel(edmModel);
            var filteredSchemaElements = new Dictionary<string, IEdmSchemaElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var edmModelSchemaElement in edmModel.SchemaElements)
            {
                var modelSchemaElement = edmModelSchemaElement as IEdmEntityType;
                if (modelSchemaElement != null)
                {
                    var filterEdmEntityModel = new FilterEdmEntityModel(modelSchemaElement, interestedProxyTypes);
                    if (interestedProxyTypes.ContainsKey(edmModelSchemaElement.Name))
                    {
                        filteredSchemaElements.Add(edmModelSchemaElement.Name, filterEdmEntityModel);

                        if (filterEdmEntityModel.BaseType != null)
                        {
                            var baseType = filterEdmEntityModel.BaseType as IEdmEntityType;
                            if (!filteredSchemaElements.ContainsKey(baseType.Name))
                            {
                                filteredSchemaElements.Add(baseType.Name, baseType);
                            }
                        }
                    }
                }
            }

            customEdmModel.SchemaElements = filteredSchemaElements.Select(x => x.Value).AsEnumerable();
            customEdmModel.VocabularyAnnotations = edmModel.VocabularyAnnotations;
            customEdmModel.DeclaredNamespaces = edmModel.DeclaredNamespaces;
            customEdmModel.DirectValueAnnotationsManager = edmModel.DirectValueAnnotationsManager;
            customEdmModel.EntityContainer = edmModel.EntityContainer;
            customEdmModel.ReferencedModels = edmModel.ReferencedModels;

            using (var xmlTextWriter = new XmlTextWriter("CustomModel.xml", Encoding.UTF8))
            {
                CsdlWriter.TryWriteCsdl(customEdmModel, xmlTextWriter,
                    CsdlTarget.OData, out var errors);
            }
        }
    }
}

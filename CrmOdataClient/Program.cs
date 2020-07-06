using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.OData;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;

namespace CrmOdataClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var interestedProxyTypes = new Hashtable();
            interestedProxyTypes.Add("contact", "contact");
            interestedProxyTypes.Add("account", "account");

            var fileStream = File.OpenRead("metadata.xml");
            var edmModel = CsdlReader.Parse(new XmlTextReader(fileStream));

            var customEdmModel = new CustomEdmModel(edmModel);
            var filteredSchemaElements = new List<IEdmSchemaElement>();
            foreach (var edmModelSchemaElement in edmModel.SchemaElements)
            {
                if (interestedProxyTypes.ContainsKey(edmModelSchemaElement.Name))
                {
                    filteredSchemaElements.Add(edmModelSchemaElement);
                }
            }

            var filterdPropertySchemaElements =new List<IEdmSchemaElement>();
            foreach (IEdmEntityType filteredSchemaElement in filteredSchemaElements)
            {
                filterdPropertySchemaElements.Add(new FilterEdmEntityModel(filteredSchemaElement, interestedProxyTypes));
            }

            customEdmModel.SchemaElements = filterdPropertySchemaElements.AsEnumerable();
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

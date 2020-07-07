using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;

namespace CrmOdataClient
{
    public class FilterEdmEntityModel : IEdmEntityType
    {
        private readonly IEdmEntityType _model;
        private readonly Hashtable _navigationEntityTypes;

        private IList<IEdmProperty> _filteredDeclaredProperties;

        public FilterEdmEntityModel(IEdmEntityType model, Hashtable navigationEntityTypes)
        {
            _model = model;
            _navigationEntityTypes = navigationEntityTypes;
            var edmProperties = _model.DeclaredProperties
                .Where(x =>
                {
                    if (x.PropertyKind == EdmPropertyKind.None ||
                        x.PropertyKind == EdmPropertyKind.Structural)
                        return true;

                    if (x.PropertyKind == EdmPropertyKind.Navigation)
                    {
                        if (EdmTypeSemantics.IsCollection(x.Type))
                        {
                            var collectionElementType =
                                ExtensionMethods.AsElementType(x.Type.Definition) as IEdmEntityType;
                            var name = collectionElementType.Name;
                            return _navigationEntityTypes.ContainsKey(name);
                        }
                        else
                        {
                            var collectionElementType =
                                ExtensionMethods.AsElementType(x.Type.Definition) as IEdmEntityType;
                            var name = collectionElementType.Name;
                            return _navigationEntityTypes.ContainsKey(name);
                        }
                    }

                    return false;
                });
            _filteredDeclaredProperties = new List<IEdmProperty>(edmProperties);
        }

        public EdmTypeKind TypeKind => _model.TypeKind;

        public IEdmProperty FindProperty(string name)
        {
            return _model.FindProperty(name);
        }

        public bool IsAbstract => _model.IsAbstract;
        public bool IsOpen => _model.IsOpen;
        public IEdmStructuredType BaseType => _model.BaseType;
        public IEnumerable<IEdmProperty> DeclaredProperties => _filteredDeclaredProperties.AsEnumerable();
        public string Name => _model.Name;
        public EdmSchemaElementKind SchemaElementKind => _model.SchemaElementKind;
        public string Namespace => _model.Namespace;
        public IEnumerable<IEdmStructuralProperty> DeclaredKey => _model.DeclaredKey;
        public bool HasStream => _model.HasStream;


    }

    public class HttpClientRequestMessage : DataServiceClientRequestMessage
    {
        private readonly HttpRequestMessage requestMessage;
        private readonly HttpClient client;
        private readonly HttpClientHandler handler;
        private readonly MemoryStream messageStream;
        private readonly Dictionary<string, string> contentHeaderValueCache;

        public HttpClientRequestMessage(string actualMethod)
            : base(actualMethod)
        {
            this.requestMessage = new HttpRequestMessage();
            this.messageStream = new MemoryStream();
            this.handler = new HttpClientHandler();
            this.client = new HttpClient(this.handler, disposeHandler: true);
            this.contentHeaderValueCache = new Dictionary<string, string>();
        }

        public override IEnumerable<KeyValuePair<string, string>> Headers
        {
            get
            {
                if (this.requestMessage.Content != null)
                {
                    return HttpHeadersToStringDictionary(this.requestMessage.Headers).Concat(HttpHeadersToStringDictionary(this.requestMessage.Content.Headers));
                }

                return HttpHeadersToStringDictionary(this.requestMessage.Headers).Concat(this.contentHeaderValueCache);
            }
        }

        public override Uri Url
        {
            get { return requestMessage.RequestUri; }
            set { requestMessage.RequestUri = value; }
        }

        public override string Method
        {
            get { return this.requestMessage.Method.ToString(); }
            set { this.requestMessage.Method = new HttpMethod(value); }
        }

        public override ICredentials Credentials
        {
            get { return this.handler.Credentials; }
            set { this.handler.Credentials = value; }
        }

        public override int Timeout
        {
            get { return (int)this.client.Timeout.TotalSeconds; }
            set { this.client.Timeout = new TimeSpan(0, 0, value); }
        }

        public override int ReadWriteTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether to send data in segments to the Internet resource. 
        /// </summary>
        public override bool SendChunked
        {
            get
            {
                bool? transferEncodingChunked = this.requestMessage.Headers.TransferEncodingChunked;
                return transferEncodingChunked.HasValue && transferEncodingChunked.Value;
            }
            set { this.requestMessage.Headers.TransferEncodingChunked = value; }
        }

        public override string GetHeader(string headerName)
        {
            //Returns the value of the header with the given name.
            var values = requestMessage.Headers.GetValues(headerName);
            return values.FirstOrDefault();
        }

        public override void SetHeader(string headerName, string headerValue)
        {
            requestMessage.Headers.Add(headerName, headerValue);
            // Sets the value of the header with the given name
        }

        public override Stream GetStream()
        {
            return this.messageStream;
        }

        /// <summary>
        /// Abort the current request.
        /// </summary>
        public override void Abort()
        {
            this.client.CancelPendingRequests();
        }

        public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
        {
            var taskCompletionSource = new TaskCompletionSource<Stream>();
            taskCompletionSource.TrySetResult(this.messageStream);
            return taskCompletionSource.Task.ToApm(callback, state);
        }

        public override Stream EndGetRequestStream(IAsyncResult asyncResult)
        {
            return ((Task<Stream>)asyncResult).Result;
        }

        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            var send = CreateSendTask();
            return send.ToApm(callback, state);
        }

        public override IODataResponseMessage EndGetResponse(IAsyncResult asyncResult)
        {
            var result = ((Task<HttpResponseMessage>)asyncResult).Result;
            return ConvertHttpClientResponse(result);
        }

        public override IODataResponseMessage GetResponse()
        {
            var send = CreateSendTask();
            send.Wait();
            return ConvertHttpClientResponse(send.Result);
        }

        private Task<HttpResponseMessage> CreateSendTask()
        {
            // Only set the message content if the stream has been written to, otherwise
            // HttpClient will complain if it's a GET request.
            var messageContent = this.messageStream.ToArray();
            if (messageContent.Length > 0)
            {
                this.requestMessage.Content = new ByteArrayContent(messageContent);

                // Apply cached "Content" header values
                foreach (var contentHeader in this.contentHeaderValueCache)
                {
                    this.requestMessage.Content.Headers.Add(contentHeader.Key, contentHeader.Value);
                }
            }

            this.requestMessage.Method = new HttpMethod(this.ActualMethod);

            var send = this.client.SendAsync(this.requestMessage);
            return send;
        }

        private static IDictionary<string, string> HttpHeadersToStringDictionary(HttpHeaders headers)
        {
            return headers.ToDictionary((h1) => h1.Key, (h2) => string.Join(",", h2.Value));
        }

        private static HttpClientResponseMessage ConvertHttpClientResponse(HttpResponseMessage response)
        {
            return new HttpClientResponseMessage(response);
        }
    }

    public static class TaskExtensionMethods
    {
        public static Task<TResult> ToApm<TResult>(this Task<TResult> task, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<TResult>(state);

            task.ContinueWith(
                delegate
                {
                    if (task.IsFaulted)
                    {
                        tcs.TrySetException(task.Exception.InnerExceptions);
                    }
                    else if (task.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(task.Result);
                    }

                    if (callback != null)
                    {
                        callback(tcs.Task);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            return tcs.Task;
        }
    }

    public class HttpClientResponseMessage : IODataResponseMessage, IDisposable
    {
        private readonly IDictionary<string, string> headers;
        private readonly Func<Stream> getResponseStream;
        private readonly int statusCode;

#if DEBUG
        /// <summary>set to true once the GetStream was called.</summary>
        private bool streamReturned;
#endif

        public HttpClientResponseMessage(HttpResponseMessage httpResponse)
        {
            this.headers = HttpHeadersToStringDictionary(httpResponse.Headers);
            this.statusCode = (int)httpResponse.StatusCode;
            this.getResponseStream = () => { var task = httpResponse.Content.ReadAsStreamAsync(); task.Wait(); return task.Result; };
        }

        private static IDictionary<string, string> HttpHeadersToStringDictionary(HttpHeaders headers)
        {
            return headers.ToDictionary((h1) => h1.Key, (h2) => string.Join(",", h2.Value));
        }

        /// <summary>
        /// Returns the collection of response headers.
        /// </summary>
        public virtual IEnumerable<KeyValuePair<string, string>> Headers
        {
            get { return this.headers; }
        }

        /// <summary>
        /// The response status code.
        /// </summary>
        public virtual int StatusCode
        {
            get { return this.statusCode; }

            set { throw new NotSupportedException(); }
        }

        public virtual string GetHeader(string headerName)
        {
            string result;
            if (this.headers.TryGetValue(headerName, out result))
            {
                return result;
            }

            // Since the unintialized value of ContentLength header is -1, we need to return
            // -1 if the content length header is not present
            if (string.Equals(headerName, "Content-Length", StringComparison.Ordinal))
            {
                return "-1";
            }

            return null;
        }

        public virtual void SetHeader(string headerName, string headerValue)
        {
            if (String.IsNullOrEmpty(headerValue))
            {
                return;
            }
            if (this.headers.ContainsKey(headerName))
            {
                this.headers[headerName] = headerValue;
            }
            else
            {
                this.headers.Add(headerName, headerValue);
            }
        }

        public virtual Stream GetStream()
        {
#if DEBUG
            Debug.Assert(!this.streamReturned, "The GetStream can only be called once.");
            this.streamReturned = true;
#endif

            return this.getResponseStream();
        }

        public void Dispose()
        {
        }
    }

}
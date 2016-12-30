﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Net;
using System.Collections;
using System.Threading.Tasks;

namespace PubnubApi
{
    public class PubnubHttp : IPubnubHttp
    {
        private static PNConfiguration pubnubConfig = null;
        private static IJsonPluggableLibrary jsonLib = null;

        public PubnubHttp(PNConfiguration config, IJsonPluggableLibrary jsonPluggableLibrary)
        {
            pubnubConfig = config;
            jsonLib = jsonPluggableLibrary;
        }

        HttpWebRequest IPubnubHttp.SetProxy<T>(HttpWebRequest request)
        {
#if !NETSTANDARD10
            if (pubnubConfig.Proxy != null)
            {
                request.Proxy = pubnubConfig.Proxy;
            }
#endif
            return request;
        }

        HttpWebRequest IPubnubHttp.SetTimeout<T>(RequestState<T> pubnubRequestState, HttpWebRequest request)
        {
#if NET35 || NET40
            request.Timeout = GetTimeoutInSecondsForResponseType(pubnubRequestState.ResponseType) * 1000;
#endif
            return request;
        }


        HttpWebRequest IPubnubHttp.SetServicePointSetTcpKeepAlive(HttpWebRequest request)
        {
#if ((!__MonoCS__) && (!SILVERLIGHT) && !WINDOWS_PHONE && !NETFX_CORE)
            //request.ServicePoint.SetTcpKeepAlive(true, base.LocalClientHeartbeatInterval * 1000, 1000);
#endif
            //do nothing for mono
            return request;
        }

        async Task<string> IPubnubHttp.SendRequestAndGetJsonResponse<T>(Uri requestUri, RequestState<T> pubnubRequestState, HttpWebRequest request)
        {
            HttpWebResponse response = null;
            System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Before Task.Factory.FromAsync", DateTime.Now.ToString()));
            try
            {
                request.Method = "GET";
                response = await Task.Factory.FromAsync<HttpWebResponse>(request.BeginGetResponse, asyncPubnubResult => (HttpWebResponse)request.EndGetResponse(asyncPubnubResult), pubnubRequestState);
                pubnubRequestState.Response = response;
                System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Got PubnubWebResponse for {1}", DateTime.Now.ToString(), request.RequestUri.ToString()));
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    //Need to return this response 
#if NET35
                    string jsonString = streamReader.ReadToEnd();
#else
                    string jsonString = await streamReader.ReadToEndAsync();
#endif
                    System.Diagnostics.Debug.WriteLine(jsonString);
                    System.Diagnostics.Debug.WriteLine("");
                    System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Retrieved JSON", DateTime.Now.ToString()));
                    return jsonString;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    pubnubRequestState.Response = ex.Response as HttpWebResponse;
                    using (StreamReader streamReader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        //Need to return this response 
#if NET35
                        string jsonString = streamReader.ReadToEnd();
#else
                        string jsonString = await streamReader.ReadToEndAsync();
#endif
                        System.Diagnostics.Debug.WriteLine(jsonString);
                        System.Diagnostics.Debug.WriteLine("");
                        System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Retrieved JSON from WebException response", DateTime.Now.ToString()));
                        return jsonString;
                    }
                }

                if (ex.Message.IndexOf("The request was aborted: The request was canceled") == -1
                                && ex.Message.IndexOf("Machine suspend mode enabled. No request will be processed.") == -1)
                {
                    throw ex;
                }
                return "";
            }
            catch (Exception ex)
            {
                throw ex;
            }
            //return task.ContinueWith(t => ReadStreamFromResponse(t.Result));

            /*
            System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Before BeginGetResponse", DateTime.Now.ToString()));
            var taskComplete = new TaskCompletionSource<string>();

            IAsyncResult asyncResult = request.BeginGetResponse(new AsyncCallback(
                (asynchronousResult) => {
                    RequestState<T> asyncRequestState = asynchronousResult.AsyncState as RequestState<T>;
                    PubnubWebRequest asyncWebRequest = asyncRequestState.Request as PubnubWebRequest;
                    if (asyncWebRequest != null)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Before EndGetResponse", DateTime.Now.ToString()));
                        PubnubWebResponse asyncWebResponse = (PubnubWebResponse)asyncWebRequest.EndGetResponse(asynchronousResult);
                        System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, After EndGetResponse", DateTime.Now.ToString()));
                        using (StreamReader streamReader = new StreamReader(asyncWebResponse.GetResponseStream()))
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Inside StreamReader", DateTime.Now.ToString()));
                            //Need to return this response 
                            string jsonString = streamReader.ReadToEnd();
                            System.Diagnostics.Debug.WriteLine(jsonString);
                            System.Diagnostics.Debug.WriteLine("");
                            System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Retrieved JSON", DateTime.Now.ToString()));
                            taskComplete.TrySetResult(jsonString);
                        }
                    }
                }
                ), pubnubRequestState);

            Timer webRequestTimer = new Timer(OnPubnubWebRequestTimeout<T>, pubnubRequestState, GetTimeoutInSecondsForResponseType(pubnubRequestState.ResponseType) * 1000, Timeout.Infinite);

            return taskComplete.Task;
            */
        }

        async Task<string> IPubnubHttp.SendRequestAndGetJsonResponseWithPOST<T>(Uri requestUri, RequestState<T> pubnubRequestState, HttpWebRequest request, string postData)
        {
            //HttpWebResponse response = null;
            System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Before Task.Factory.FromAsync With POST", DateTime.Now.ToString()));
            try
            {
                request.Method = "POST";
                request.ContentType = "application/json";

                byte[] data = Encoding.UTF8.GetBytes(postData);
                //request.ContentLength = data.Length;
                using (var requestStream = await Task<Stream>.Factory.FromAsync(request.BeginGetRequestStream, request.EndGetRequestStream, pubnubRequestState))
                {
#if NET35
                    requestStream.Write(data, 0, data.Length);
                    requestStream.Flush();
#else
                    await requestStream.WriteAsync(data, 0, data.Length);
                    await requestStream.FlushAsync();
#endif

                }

                WebResponse response = await Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, pubnubRequestState);
                pubnubRequestState.Response = response as HttpWebResponse;
                System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Got PubnubWebResponse With POST for {1}", DateTime.Now.ToString(), request.RequestUri.ToString()));
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    //Need to return this response 
#if NET35
                    string jsonString = streamReader.ReadToEnd();
#else
                    string jsonString = await streamReader.ReadToEndAsync();
#endif
                    System.Diagnostics.Debug.WriteLine(jsonString);
                    System.Diagnostics.Debug.WriteLine("");
                    System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Retrieved JSON With POST", DateTime.Now.ToString()));
                    return jsonString;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    pubnubRequestState.Response = ex.Response as HttpWebResponse;
                    using (StreamReader streamReader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        //Need to return this response 
#if NET35
                        string jsonString = streamReader.ReadToEnd();
#else
                        string jsonString = await streamReader.ReadToEndAsync();
#endif
                        System.Diagnostics.Debug.WriteLine(jsonString);
                        System.Diagnostics.Debug.WriteLine("");
                        System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Retrieved JSON  With POST from WebException response", DateTime.Now.ToString()));
                        return jsonString;
                    }
                }

                if (ex.Message.IndexOf("The request was aborted: The request was canceled") == -1
                                && ex.Message.IndexOf("Machine suspend mode enabled. No request will be processed.") == -1)
                {
                    throw ex;
                }
                return "";
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //private string ReadStreamFromResponse(HttpWebResponse response)
        //{
        //    System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Got PubnubWebResponse", DateTime.Now.ToString()));
        //    using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
        //    {
        //        //Need to return this response 
        //        string jsonString = streamReader.ReadToEnd();
        //        System.Diagnostics.Debug.WriteLine(jsonString);
        //        System.Diagnostics.Debug.WriteLine("");
        //        System.Diagnostics.Debug.WriteLine(string.Format("DateTime {0}, Retrieved JSON", DateTime.Now.ToString()));
        //        return jsonString;
        //    }
        //}

        protected void OnPubnubWebRequestTimeout<T>(object state, bool timeout)
        {
            if (timeout && state != null)
            {
                RequestState<T> currentState = state as RequestState<T>;
                if (currentState != null)
                {
                    HttpWebRequest request = currentState.Request;
                    if (request != null)
                    {
                        string currentMultiChannel = (currentState.Channels == null) ? "" : string.Join(",", currentState.Channels);
                        string currentMultiChannelGroup = (currentState.ChannelGroups == null) ? "" : string.Join(",", currentState.ChannelGroups);
                        LoggingMethod.WriteToLog(string.Format("DateTime: {0}, OnPubnubWebRequestTimeout: client request timeout reached.Request abort for channel={1} ;channelgroup={2}", DateTime.Now.ToString(), currentMultiChannel, currentMultiChannelGroup), pubnubConfig.LogVerbosity);
                        currentState.Timeout = true;
                        //TerminatePendingWebRequest(currentState);
                    }
                }
                else
                {
                    LoggingMethod.WriteToLog(string.Format("DateTime: {0}, OnPubnubWebRequestTimeout: client request timeout reached. However state is unknown", DateTime.Now.ToString()), pubnubConfig.LogVerbosity);
                }
            }
        }

        protected void OnPubnubWebRequestTimeout<T>(System.Object requestState)
        {
            RequestState<T> currentState = requestState as RequestState<T>;
            if (currentState != null && currentState.Response == null && currentState.Request != null)
            {
                currentState.Timeout = true;
                //TerminatePendingWebRequest(currentState);
                LoggingMethod.WriteToLog(string.Format("DateTime: {0}, **WP7 OnPubnubWebRequestTimeout**", DateTime.Now.ToString()), pubnubConfig.LogVerbosity);
            }
        }

        protected int GetTimeoutInSecondsForResponseType(PNOperationType type)
        {
            int timeout;
            if (type == PNOperationType.PNSubscribeOperation || type == PNOperationType.Presence)
            {
                timeout = pubnubConfig.SubscribeTimeout;
            }
            else
            {
                timeout = pubnubConfig.NonSubscribeRequestTimeout;
            }
            return timeout;
        }


    }
}

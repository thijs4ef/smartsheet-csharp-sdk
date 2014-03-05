﻿using System;
using System.Collections.Generic;

namespace Smartsheet.Api.Internal
{

    using Api.Internal.json;
    using Api.Models;
    using System.IO;
    using System.Net;

    /*
    * #[license]
    * Smartsheet SDK for C#
    * %%
    * Copyright (C) 2014 Smartsheet
    * %%
    * Licensed under the Apache License, Version 2.0 (the "License");
    * you may not use this file except in compliance with the License.
    * You may obtain a copy of the License at
    * 
    *      http://www.apache.org/licenses/LICENSE-2.0
    * 
    * Unless required by applicable law or agreed To in writing, software
    * distributed under the License is distributed on an "AS IS" BASIS,
    * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    * See the License for the specific language governing permissions and
    * limitations under the License.
    * %[license]
    */



    //using JsonParseException = fasterxml.jackson.core.JsonParseException;
    //using JsonMappingException = fasterxml.jackson.databind.JsonMappingException;
    using HttpEntity = Api.Internal.http.HttpEntity;
    using HttpMethod = Api.Internal.http.HttpMethod;
    using HttpRequest = Api.Internal.http.HttpRequest;
    using HttpResponse = Api.Internal.http.HttpResponse;
    using Util = Api.Internal.util.Util;

	/// <summary>
	/// This is the base class of the Smartsheet REST API resources.
	/// 
	/// Thread Safety: This class is thread safe because it is immutable and the underlying SmartsheetImpl is thread safe.
	/// </summary>
	public abstract class AbstractResources
	{
        public class ErrorCode{
            public static readonly ErrorCode BAD_REQUEST = new ErrorCode(HttpStatusCode.BadRequest, typeof(Api.InvalidRequestException));
            public static readonly ErrorCode NOT_AUTHORIZED = new ErrorCode(HttpStatusCode.Unauthorized, typeof(Api.AuthorizationException));
            public static readonly ErrorCode FORBIDDEN = new ErrorCode(HttpStatusCode.Forbidden, typeof(Api.AuthorizationException));
            public static readonly ErrorCode NOT_FOUND = new ErrorCode(HttpStatusCode.NotFound, typeof(Api.ResourceNotFoundException));
            public static readonly ErrorCode METHOD_NOT_SUPPORTED = new ErrorCode(HttpStatusCode.MethodNotAllowed, typeof(Api.InvalidRequestException));
            public static readonly ErrorCode INTERNAL_SERVER_ERROR = new ErrorCode(HttpStatusCode.InternalServerError, typeof(Api.InvalidRequestException));
            public static readonly ErrorCode SERVICE_UNAVAILABLE = new ErrorCode(HttpStatusCode.ServiceUnavailable, typeof(Api.ServiceUnavailableException));

            public static IEnumerable<ErrorCode> Values
            {
                get{
                    yield return BAD_REQUEST;
                    yield return NOT_AUTHORIZED;
                    yield return FORBIDDEN;
                    yield return NOT_FOUND;
                    yield return METHOD_NOT_SUPPORTED;
                    yield return INTERNAL_SERVER_ERROR;
                    yield return SERVICE_UNAVAILABLE;
                }
            }

            private readonly HttpStatusCode errorCode;
            private readonly Type exceptionClass;

            ErrorCode(HttpStatusCode errorCode, System.Type exceptionClass)
            {
                this.errorCode = errorCode;
				this.exceptionClass = exceptionClass;
            }

            			/// <summary>
			/// Gets the error Code.
			/// </summary>
			/// <param Name="errorNumber"> the error number </param>
			/// <returns> the error Code </returns>
			public static ErrorCode getErrorCode(HttpStatusCode errorNumber)
			{
				foreach (ErrorCode code in ErrorCode.Values)
				{
					if (code.errorCode == errorNumber)
					{
						return code;
					}
				}
	
				return null;
			}

            //            /// <summary>
            //            /// Gets the exception.
            //            /// </summary>
            //            /// <returns> the exception </returns>
            //            /// <exception cref="MemberAccessException"> the instantiation exception </exception>
            //            /// <exception cre="IllegalAccessException"> the illegal access exception </exception>
            public SmartsheetRestException getException()
            {
                return (SmartsheetRestException)Activator.CreateInstance(exceptionClass);
            }

            //            /// <summary>
            //            /// Gets the exception.
            //            /// </summary>
            //            /// <param Name="error"> the error </param>
            //            /// <returns> the exception </returns>
            //            /// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
            public SmartsheetRestException getException(Api.Models.Error error)
            {
                object[] args = new object[]{error};
                return (SmartsheetRestException)Activator.CreateInstance(exceptionClass, args);
            }
        }

		/// <summary>
		/// Represents the SmartsheetImpl.
		/// 
		/// It will be initialized in constructor and will not change afterwards.
		/// </summary>
		private SmartsheetImpl smartsheet;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param Name="Smartsheet"> the Smartsheet </param>
		protected internal AbstractResources(SmartsheetImpl smartsheet)
		{
			Util.ThrowIfNull(smartsheet);

			this.smartsheet = smartsheet;
		}

		/// <summary>
		/// Get a resource from Smartsheet REST API.
		/// 
		/// Parameters: - path : the relative path of the resource - objectClass : the resource object class
		/// 
		/// Returns: the resource (note that if there is no such resource, this method will throw ResourceNotFoundException
		/// rather than returning null).
		/// 
		/// Exceptions: -
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ResourceNotFoundException : if the resource can not be found
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// <param Name="path"> the relative path of the resource. </param>
		/// <param Name="objectClass"> the object class </param>
		/// <returns> the resource </returns>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
		protected internal virtual T GetResource<T>(string path, Type objectClass)
		{
			Util.ThrowIfNull(path, objectClass);

			if (path == null || path.Length == 0)
			{
				Api.Models.Error error = new Api.Models.Error();
				error.Message = "An empty path was provided.";
				throw new ResourceNotFoundException(error);
			}

			HttpRequest request = null;
			try
			{       
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI,path), HttpMethod.GET);
			}
			catch (Exception e)
			{
				throw new SmartsheetException(e);
			}

            HttpResponse response = this.smartsheet.HttpClient.Request(request);
            

			Object obj = null;
            
			switch (response.StatusCode)
			{
                case HttpStatusCode.OK:
					try
					{
                        obj = this.smartsheet.JsonSerializer.deserialize<T>(response.Entity.getContent());
					}
                    catch (JSONSerializationException ex)
					{
						throw new SmartsheetException(ex);
					}
                    catch (Newtonsoft.Json.JsonException ex)
                    {
                        throw new SmartsheetException(ex);
                    }
                    catch (IOException ex)
                    {
                        throw new SmartsheetException(ex);
                    }
				break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();

			return (T)obj;
		}

		/// <summary>
		/// Create a resource using Smartsheet REST API.
		/// 
		/// Exceptions: 
		///   IllegalArgumentException : if any argument is null, or path is empty string
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// <param Name="path"> the relative path of the resource collections </param>
		/// <param Name="objectClass"> the resource object class </param>
		/// <param Name="object"> the object To create </param>
		/// <returns> the created resource </returns>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
		protected internal virtual T CreateResource<T>(string path, Type objectClass, T @object)
		{
			Util.ThrowIfNull(path, @object);
			Util.ThrowIfEmpty(path);

			HttpRequest request = null;
			try
			{
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI, path), HttpMethod.POST);
			}
            catch (Exception e)
			{
				throw new SmartsheetException(e);
			}

            request.Entity = serializeToEntity<T>(@object);

			HttpResponse response = this.smartsheet.HttpClient.Request(request);

            Object obj = null;
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
                    obj = this.smartsheet.JsonSerializer.deserializeResult<T>(
                        response.Entity.getContent()).ResultObject;
					break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();

			return (T)obj;
		}

		/// <summary>
		/// Update a resource using Smartsheet REST API.
		/// 
		/// Exceptions:
		///   IllegalArgumentException : if any argument is null, or path is empty string
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ResourceNotFoundException : if the resource can not be found
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// <param Name="path"> the relative path of the resource </param>
		/// <param Name="objectClass"> the resource object class </param>
		/// <param Name="object"> the object To create </param>
		/// <returns> the updated resource </returns>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
		protected internal virtual T UpdateResource<T>(string path, Type objectClass, T @object)
		{
			Util.ThrowIfNull(path, @object);
			Util.ThrowIfEmpty(path);

			HttpRequest request = null;
			try
			{
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI, path), HttpMethod.PUT);
			}
			catch (Exception e)
			{
				throw new SmartsheetException(e);
			}

            request.Entity = request.Entity = serializeToEntity<T>(@object);

			HttpResponse response = this.smartsheet.HttpClient.Request(request);

            Object obj = null;
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
					//obj = this.smartsheet.JsonSerializer.DeserializeResult(objectClass, response.Entity.content).Result;
                    obj = this.smartsheet.JsonSerializer.deserializeResult<T>(response.Entity.getContent()).ResultObject;
					break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();

			return (T)obj;
		}

		/// <summary>
		/// List resources using Smartsheet REST API.
		/// 
		/// Exceptions:
		///   IllegalArgumentException : if any argument is null, or path is empty string
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// <param Name="path"> the relative path of the resource collections </param>
		/// <param Name="objectClass"> the resource object class </param>
		/// <returns> the resources </returns>
		/// <exception cref="SmartsheetException"> if an error occurred during the operation </exception>
		protected internal virtual IList<T> ListResources<T>(string path, Type objectClass)
		{
			Util.ThrowIfNull(path, objectClass);
			Util.ThrowIfEmpty(path);

			HttpRequest request = null;
			try
			{
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI, path), HttpMethod.GET);
			}
			catch (Exception e)
			{
				throw new SmartsheetException(e);
			}

			HttpResponse response = this.smartsheet.HttpClient.Request(request);

			IList<T> obj = null;
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
					//obj = this.smartsheet.JsonSerializer.DeserializeList(objectClass, response.Entity.content);
                    obj = this.smartsheet.JsonSerializer.deserializeList<T>(response.Entity.getContent());
					break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();

			return obj;
		}

		/// <summary>
		/// Delete a resource from Smartsheet REST API.
		/// 
		/// Exceptions:
		///   IllegalArgumentException : if any argument is null, or path is empty string
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ResourceNotFoundException : if the resource can not be found
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// <param Name="path"> the relative path of the resource </param>
		/// <param Name="objectClass"> the resource object class </param>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
		protected internal virtual void DeleteResource<T>(string path, Type objectClass)
		{
			Util.ThrowIfNull(path, objectClass);
			Util.ThrowIfEmpty(path);

			HttpRequest request = null;
			try
			{
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI, path), HttpMethod.DELETE);
			}
			catch (Exception e)
			{
				throw new SmartsheetException(e);
			}
			HttpResponse response = this.smartsheet.HttpClient.Request(request);

			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
                    this.smartsheet.JsonSerializer.deserializeResult<T>(response.Entity.getContent());
					break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();
		}

		/// <summary>
		/// Post an object To Smartsheet REST API and receive a list of objects from response.
		/// 
		/// Parameters: - path : the relative path of the resource collections - objectToPost : the object To post -
		/// objectClassToReceive : the resource object class To receive
		/// 
		/// Returns: the object list
		/// 
		/// Exceptions:
		///   IllegalArgumentException : if any argument is null, or path is empty string
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// @param <S> the generic Type </param>
		/// <param Name="path"> the path </param>
		/// <param Name="objectToPost"> the object To post </param>
		/// <param Name="objectClassToReceive"> the object class To receive </param>
		/// <returns> the list </returns>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
		protected internal virtual IList<S> PostAndReceiveList<T, S>(string path, T objectToPost, Type objectClassToReceive)
		{
			Util.ThrowIfNull(path, objectToPost, objectClassToReceive);
			Util.ThrowIfEmpty(path);

			HttpRequest request = null;
			try
			{
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI, path), HttpMethod.POST);
			}
			catch (Exception e)
			{
				throw new SmartsheetException(e);
			}

            request.Entity = serializeToEntity<T>(objectToPost);

			HttpResponse response = this.smartsheet.HttpClient.Request(request);

			IList<S> obj = null;
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
                    obj = this.smartsheet.JsonSerializer.deserializeListResult<S>(
                        response.Entity.getContent()).ResultObject;
					break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();

			return obj;
		}

		/// <summary>
		/// Put an object To Smartsheet REST API and receive a list of objects from response.
		/// 
		/// Exceptions:
		///   IllegalArgumentException : if any argument is null, or path is empty string
		///   InvalidRequestException : if there is any problem with the REST API request
		///   AuthorizationException : if there is any problem with the REST API authorization(access token)
		///   ServiceUnavailableException : if the REST API service is not available (possibly due To rate limiting)
		///   SmartsheetRestException : if there is any other REST API related error occurred during the operation
		///   SmartsheetException : if there is any other error occurred during the operation
		/// </summary>
		/// @param <T> the generic Type </param>
		/// @param <S> the generic Type </param>
		/// <param Name="path"> the relative path of the resource collections </param>
		/// <param Name="objectToPut"> the object To put </param>
		/// <param Name="objectClassToReceive"> the resource object class To receive </param>
		/// <returns> the object list </returns>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
        protected internal virtual IList<S> PutAndReceiveList<T, S>(string path, T objectToPut, Type objectClassToReceive)
		{
			Util.ThrowIfNull(path, objectToPut, objectClassToReceive);
			Util.ThrowIfEmpty(path);

			HttpRequest request = null;
			try
			{
                request = CreateHttpRequest(new Uri(smartsheet.BaseURI, path), HttpMethod.PUT);
			}
			catch (Exception e)
			{
				throw new SmartsheetException(e);
			}

            request.Entity = serializeToEntity<T>(objectToPut);

			HttpResponse response = this.smartsheet.HttpClient.Request(request);

			IList<S> obj = null;
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
                    obj = this.smartsheet.JsonSerializer.deserializeListResult<S>( 
                        response.Entity.getContent()).ResultObject;
					break;
				default:
					HandleError(response);
				break;
			}

			smartsheet.HttpClient.ReleaseConnection();

			return obj;
		}

		/// <summary>
		/// Create an HttpRequest.
		/// 
		/// Exceptions: Any exception shall be propagated since it's a private method.
		/// </summary>
		/// <param Name="uri"> the URI </param>
		/// <param Name="method"> the HttpMethod </param>
		/// <returns> the http request </returns>
		protected internal virtual HttpRequest CreateHttpRequest(Uri uri, HttpMethod method)
		{
			HttpRequest request = new HttpRequest();
			request.Uri = uri;
			request.Method = method;

			// Set authorization header 
			request.Headers = new Dictionary<string, string>();
			request.Headers["Authorization"] = "Bearer " + smartsheet.AccessToken;

			// Set assumed user
			if (smartsheet.AssumedUser != null)
			{
				request.Headers["Assume-User"] = Uri.EscapeDataString(smartsheet.AssumedUser);
			}

			return request;
		}

		/// <summary>
		/// Handles an error HttpResponse (non-200) returned by Smartsheet REST API.
		/// 
		/// Exceptions: 
		///   SmartsheetRestException : the exception corresponding To the error
		/// </summary>
		/// <param Name="response"> the HttpResponse </param>
		/// <exception cref="SmartsheetException"> the Smartsheet exception </exception>
		protected internal virtual void HandleError(HttpResponse response)
		{

			Api.Models.Error error;
			try
			{
                error = this.smartsheet.JsonSerializer.deserialize<Api.Models.Error>(
                    response.Entity.getContent());
            }
            catch (JSONSerializationException ex)
            {
                throw new SmartsheetException(ex);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new SmartsheetException(ex);
            }
            catch (IOException ex)
            {
                throw new SmartsheetException(ex);
            }
            
			ErrorCode code = ErrorCode.getErrorCode(response.StatusCode);

			if (code == null)
			{
				throw new SmartsheetRestException(error);
			}

			try
			{
				throw code.getException(error);
			}
			catch (System.ArgumentException ex)
			{
				throw new SmartsheetException(ex);
			}
			catch (Exception ex)
			{
				throw new SmartsheetException(ex);
			}
		}


		/// <summary>
		/// Gets the Smartsheet.
		/// </summary>
		/// <returns> the Smartsheet </returns>
		public virtual SmartsheetImpl Smartsheet
		{
			get
			{
				return smartsheet;
			}
			set
			{
				this.smartsheet = value;
			}
		}

        protected HttpEntity serializeToEntity<T>(T objectToPost)
        {
            HttpEntity entity = new HttpEntity();
            entity.ContentType = "application/json";
            using (StreamWriter writer = new StreamWriter(new MemoryStream()))
            {
                this.smartsheet.JsonSerializer.serialize<T>(objectToPost, writer);
                writer.Flush();

                using (StreamReader reader = new StreamReader(writer.BaseStream))
                {
                    entity.Content = reader.ReadToEnd();
                    entity.ContentLength = reader.BaseStream.Length;
                }
            }
            return entity;
        }
	}
}
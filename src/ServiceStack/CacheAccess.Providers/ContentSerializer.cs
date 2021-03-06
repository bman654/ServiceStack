using System;
using System.IO;
using System.Text;
using ServiceStack.Common.Extensions;
using ServiceStack.Common.Web;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceModel.Serialization;
using ServiceStack.Text;
using StreamExtensions = ServiceStack.Common.StreamExtensions;

namespace ServiceStack.CacheAccess.Providers
{
	/// <summary>
	/// Encapsulates the behaviour of serving compressed or uncompressed 
	/// serialized results for a particular MimeType and CompressionType.
	/// </summary>
	public sealed class ContentSerializer<T>
		where T : class
	{
		public Func<T> FactoryFn { get; private set; }
		public string CompressionType { get; private set; }
		public string ContentType { get; private set; }
		public IRequestContext SerializationContext { get; private set; }

		public ContentSerializer(Func<T> factoryFn, string contentType)
		{
			factoryFn.ThrowIfNull("factoryFn");
			contentType.ThrowIfNull("contentType");

			this.FactoryFn = factoryFn;
			this.ContentType = contentType;
			this.SerializationContext = new SerializationContext(contentType);
		}

		public ContentSerializer(Func<T> factoryFn, IRequestContext serializationContext)
		{
			factoryFn.ThrowIfNull("factoryFn");
			serializationContext.ThrowIfNull("serializationContext");

			this.FactoryFn = factoryFn;
			this.ContentType = serializationContext.ResponseContentType;
			this.CompressionType = serializationContext.CompressionType;
			this.SerializationContext = serializationContext;
		}

		public bool DoCompress
		{
			get
			{
				return !CompressionType.IsNullOrEmpty();
			}
		}

		public object ToSerializedResult()
		{
			return ToSerializedString(FactoryFn(), this.SerializationContext);
		}

		public string ToSerializedString()
		{
			return ToSerializedString(FactoryFn(), this.SerializationContext);
		}

		public byte[] ToCompressedResult()
		{
			return ToCompressedResult(FactoryFn(), ContentType, CompressionType);
		}

		public static byte[] ToCompressedResult(object result, string contentType, string compressionType)
		{
			result.ThrowIfNull("result");
			contentType.ThrowIfNull("MimeType");

			var serializeCtx = new SerializationContext(contentType) { CompressionType = compressionType };
			return ToCompressedResult(ToSerializedString(result, serializeCtx), compressionType);
		}

		public static byte[] ToCompressedResult(string serializedResult, string compressionType)
		{
			if (serializedResult == null) return null;

			var compressedResult = StreamExtensions.Compress(serializedResult, compressionType);

			return compressedResult;
		}

		public static string ToSerializedString(object result, IRequestContext requestContext)
		{
			if (result == null) return null;

			var contentType = requestContext.ResponseContentType;
			var contentFilters = ContentCacheManager.ContentTypeFilter;
			if (contentFilters != null)
			{
				var serializer = contentFilters.GetStreamSerializer(contentType);
				if (serializer != null)
				{
					try
					{
						using (var ms = new MemoryStream())
						{
							serializer(requestContext, result, ms);
							var bytes = ms.ToArray();
							return Encoding.UTF8.GetString(bytes);
						}
					}
					catch (Exception ex)
					{
						throw ex;
					}
				}
			}

			switch (contentType)
			{
				case MimeTypes.Xml:
				case Common.Web.ContentType.Xml:
					return DataContractSerializer.Instance.Parse(result);

				case MimeTypes.Json:
				case Common.Web.ContentType.Json:
					return JsonDataContractSerializer.Instance.SerializeToString(result);

				case MimeTypes.Jsv:
				case Common.Web.ContentType.Jsv:
					return TypeSerializer.SerializeToString(result);

				case MimeTypes.Csv:
					return CsvSerializer.SerializeToString(result);

				default:
					throw new NotSupportedException(contentType);
			}
		}

		public static object ToOptimizedResult(string contentType, string compressionType, T result)
		{
			var serializeCtx = new SerializationContext(contentType) { CompressionType = compressionType };
			return compressionType == null
				? (object)ToSerializedString(result, serializeCtx)
				: ToCompressedResult(result, contentType, compressionType);
		}

	}
}
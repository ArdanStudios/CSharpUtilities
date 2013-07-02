#region Namespaces

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

#endregion

namespace ArdanStudios.Common
{
    /// <summary> Used to serialize c# objects to JSON and XML 
    /// Don't use the XML support
    /// I should switch to JSON.net eventually </summary>
	public static class Serializer
	{
	    #region Properties
	    
	    // <summary> Removes namespaces from the produced xml </summary>
	    // private static string XmlRegExCleanup = "( xmlns([=|:|a-z]+)\"([^\"]+)\")|( i:nil=\"([^\"]+)\")";
	    
	    /// <summary> Locates the parts of a microsoft compliant json date </summary>
	    private static string JsonDateFinder = "Date\\((?<utc>\\d+)(?<op>[-|+]{1})(?<local>\\d{4})\\)";
	    
	    /// <summary> The number of milliseconds per hour </summary>
	    private static long MillisecondsPerMinute = 60 * 1000;
	    
	    /// <summary> The number of milliseconds per hour </summary>
	    private static long MillisecondsPerHour = 60 * MillisecondsPerMinute;
	    
        #endregion        
                
	    #region DataContract Serialization and Deserialization
	    
	    /// <summary> Called by the match evaluator to fix json dates </summary>
	    /// <param name="match"></param>
	    /// <returns> The replacement string </returns>
	    private static string FixJsonDate(Match match)
	    {
	        // Date(1192010400000-1000) -> Date(1191974400000)
	        
	        long utc = long.Parse(match.Groups["utc"].Value);
	        long localHour = long.Parse(match.Groups["local"].Value.Substring(0, 2));   // First two numbers are hour
	        long localMin = long.Parse(match.Groups["local"].Value.Substring(2, 2));   // Last two numbers are minute
	        
	        long timeOffset = (localHour * MillisecondsPerHour) + (localMin * MillisecondsPerMinute);
	        
	        if (match.Groups["op"].Value[0] == '-')
	        {
	            utc = utc - timeOffset;
	        }
	        else
	        {
	            utc = utc + timeOffset;
	        }
	        
	        return string.Format("Date({0})", utc);
	    }
	    
		/// <summary> Seializes an Object of any type to JSON. Object must have proper attributes in its member declaration.
		/// [DataContract] and [DataMember] attributes need to be on the object </summary>
		/// <typeparam name="objType"> Type of Object to serialize </typeparam>
		/// <param name="oType"> Actual Object to serialize </param>
        /// <param name="knownTypes"></param>
		/// <returns> A json string </returns>
		public static string toJSON<objType>(objType oType, List<Type> knownTypes = null) 
		{
			StringBuilder outPut = new StringBuilder();
			
			try
			{
				if (oType != null)
				{
					using (MemoryStream MemStream = new MemoryStream())
					{
						// Serialize the Registration object to a memory stream using DataContractJsonSerializer.
						DataContractJsonSerializer JSONSerializer = new DataContractJsonSerializer(typeof(objType), knownTypes);
						JSONSerializer.WriteObject(MemStream, oType);
				
						MemStream.Position = 0;
						using (StreamReader Reader = new StreamReader(MemStream))
						{
							outPut.Append(Reader.ReadToEnd());
						}
					}
				}
				
				return Regex.Replace(outPut.ToString(), JsonDateFinder, new MatchEvaluator(Serializer.FixJsonDate), RegexOptions.Compiled);
			}
			
			catch
			{
				return null;				
			}
		}

        /// <summary>
        /// Returns Serialized Encrypted JSON String.
        /// </summary>
        /// <typeparam name="objType"></typeparam>
        /// <param name="oType"></param>
        /// <returns></returns>
        public static string toJSONEncrypted<objType>(objType oType)
        {
            return CryptoProvider.EncryptText(toJSON<objType>(oType));
        }

        /// <summary>
        /// Deserializes An Encrypted JSON Object
        /// </summary>
        /// <typeparam name="ObjType"></typeparam>
        /// <param name="JSON"></param>
        /// <returns></returns>
        public static ObjType toObjectDecrypted<ObjType>(string JSON) where ObjType : class
        {
            return toObject<ObjType>(CryptoProvider.DecryptText(JSON));
        }

		/// <summary> Takes a JSON string and De-serializes it to an Object </summary>
		/// <typeparam name="ObjType"> Type of Object to De-serialize </typeparam>
		/// <param name="JSON"> JSON string of object </param>
		/// <returns> An object of specified type </returns>
		public static ObjType toObject<ObjType>(string JSON) where ObjType : class
		{
			ObjType tObj;
			
			try
			{
			    byte[] bytes = ASCIIEncoding.UTF8.GetBytes(JSON);
			    
				DataContractJsonSerializer JSONSerializer = new DataContractJsonSerializer(typeof(ObjType));
				using (MemoryStream MemStream = new MemoryStream(bytes))
				{
					tObj = (ObjType) JSONSerializer.ReadObject(MemStream);
				}
				
				return tObj;
			}
			
			catch
			{
				return null;
			}
        }
        
        /// <summary> Takes a JSON string and De-serializes it to an Object </summary>
		/// <typeparam name="ObjType"> Type of Object to De-serialize </typeparam>
		/// <param name="JSON"> JSON string of object </param>
		/// <returns> An object of specified type </returns>
		public static ObjType toObject<ObjType>(byte[] JSON) where ObjType : class
		{
			ObjType tObj;
			
			try
			{
				DataContractJsonSerializer JSONSerializer = new DataContractJsonSerializer(typeof(ObjType));
				using (MemoryStream MemStream = new MemoryStream(JSON))
				{
					tObj = (ObjType) JSONSerializer.ReadObject(MemStream);
				}
				
				return tObj;
			}
			
			catch
			{
				return null;
			}
        }
        
        /// <summary> Serializes to XML </summary>
        /// <typeparam name="objType"></typeparam>
        /// <param name="oType"></param>
        /// <returns> A XML string </returns>
        public static string toXML<objType>(objType oType)
        {
            StringBuilder outPut = new StringBuilder();

            try
            {
                if (oType != null)
                {
                    using (MemoryStream MemStream = new MemoryStream())
                    {
                        DataContractSerializer XMLSerializer = new DataContractSerializer(typeof(objType));
                        XMLSerializer.WriteObject(MemStream, oType);

                        MemStream.Position = 0;
                        using (StreamReader Reader = new StreamReader(MemStream))
                        {
                            outPut.Append(Reader.ReadToEnd());
                        }
                    }
                }
                
                return outPut.ToString();
            }
            
            catch
            {
                return null;
            }
        }

        /// <summary> Takes a XML string and De-serializes it to an Object </summary>
		/// <typeparam name="ObjType"> Type of Object to De-serialize </typeparam>
		/// <param name="XML"> XML string of object </param>
		/// <returns> An object of specified type </returns>
		public static ObjType toObjectXML<ObjType>(string XML) where ObjType : class
		{
			ObjType tObj;
			
			try
			{
			    byte[] bytes = ASCIIEncoding.UTF8.GetBytes(XML);
			    
				DataContractSerializer XMLSerializer = new DataContractSerializer(typeof(ObjType));
				using (MemoryStream MemStream = new MemoryStream(bytes))
				{
					tObj = (ObjType) XMLSerializer.ReadObject(MemStream);
				}
				
				return tObj;
			}
			
			catch
			{
				return null;
			}
        }
        
        /// <summary> Takes a XML string and De-serializes it to an Object </summary>
		/// <typeparam name="ObjType"> Type of Object to De-serialize </typeparam>
		/// <param name="XML"> XML string of object </param>
	    /// <returns> An object of specified type </returns>
		public static ObjType toObjectXML<ObjType>(byte[] XML) where ObjType : class
		{
			ObjType tObj;
			
			try
			{
				DataContractSerializer XMLSerializer = new DataContractSerializer(typeof(ObjType));
				using (MemoryStream MemStream = new MemoryStream(XML))
				{
					tObj = (ObjType) XMLSerializer.ReadObject(MemStream);
				}
				
				return tObj;
			}
			
			catch
			{
				return null;
			}
        }
        
        #endregion
        
        #region Manual XML Serialization & Deserialization

        /// <summary> Deserializes an xml string to an object </summary>
        /// <param name="xml"></param>
        /// <returns> An object of specified type </returns>
        public static ObjType toXMLObject<ObjType>(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            // We get a reference to that method for the recursive calls
            MethodInfo toXMLDocMethod = typeof(Serializer).GetMethod("toXMLObject");

            Type[] typeParameters = null;
            MethodInfo generic = null;

            // to store non primitive type
            Object thisObj = null;

            // we instanciate the main class passed as generic
            ObjType obj = Activator.CreateInstance<ObjType>();

            Type type = obj.GetType();

            PropertyInfo[] pis = type.GetProperties();

            // this main node, the POSTransaction
            XmlNode posTransactionNode = doc.ChildNodes[0];

            foreach (XmlNode node in posTransactionNode.ChildNodes)
            {
                PropertyInfo pi = null;

                foreach (PropertyInfo current in pis)
                {
                    if (current.Name == node.Name)
                    {
                        pi = current;
                        break;
                    }
                }

                if (pi != null)
                {
                    string ns = node.Attributes["Ns"].InnerText;
                    string val = node.InnerText;
                    string valXml = node.OuterXml;

                    switch (ns)
                    {
                        case "System.String":

                            string valAsString = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsString = val;
                            }
                            pi.SetValue(obj, valAsString, null);
                            break;

                        case "System.Int32":

                            int? valAsInt = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsInt = int.Parse(val);
                            }
                            pi.SetValue(obj, valAsInt, null);
                            break;

                        case "System.Int64":

                            Int64? valAsInt64 = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsInt64 = Int64.Parse(val);
                            }
                            pi.SetValue(obj, valAsInt64, null);
                            break;

                        case "System.Decimal":
                            decimal? valAsDecimal = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsDecimal = Decimal.Parse(val);
                            }
                            pi.SetValue(obj, valAsDecimal, null);
                            break;

                        case "System.Float":
                            float? valAsFloat = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsFloat = float.Parse(val);
                            }
                            pi.SetValue(obj, valAsFloat, null);
                            break;

                        case "System.Collections.Generic.Dictionary":
                            break;

                        case "System.Collections.Generic.List":

                            object list = null;

                            // first, we create the list if it has not been created before
                            if (pi.GetValue(obj, null) == null)
                            {
                                typeParameters = pi.PropertyType.GetGenericArguments();

                                Type specificType = typeof(List<>).MakeGenericType(typeParameters);

                                list = Activator.CreateInstance(specificType, null);

                                pi.SetValue(obj, list, null);
                            }
                            else
                            {
                                list = pi.GetValue(obj, null);
                            }

                            // we prepare the recursive call
                            typeParameters = pi.PropertyType.GetGenericArguments();

                            if (typeParameters.Length > 0)
                            {
                                generic = toXMLDocMethod.MakeGenericMethod(typeParameters);
                            }
                            else
                            {
                                generic = toXMLDocMethod.MakeGenericMethod(pi.PropertyType);
                            }


                            // then we insert all the objects
                            foreach (XmlNode child in node.ChildNodes)
                            {

                                // not primitive type
                                thisObj = null;

                                if (!string.IsNullOrEmpty(child.OuterXml))
                                {
                                    thisObj = null;
                                    thisObj = generic.Invoke(null, new object[] { child.OuterXml });

                                    if (list != null)
                                    {
                                        // we add the object to the list
                                        ((System.Collections.IList)list).Add(thisObj);

                                    }
                                }

                            }

                            break;

                        case "System.DateTime":

                            DateTime? valAsDate = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsDate = DateTime.Parse(val);
                            }
                            pi.SetValue(obj, valAsDate, null);
                            break;

                        case "System.Boolean":

                            bool? valAsBool = null;

                            if (!string.IsNullOrEmpty(val))
                            {
                                valAsBool = bool.Parse(val);
                            }
                            pi.SetValue(obj, valAsBool, null);
                            break;

                        default:
                            // it's an enum or a plain old object

                            if (Serializer.GetBaseTypeName(pi.PropertyType) == "System.Enum")
                            {
                                Object objEnum = null;

                                if(!string.IsNullOrEmpty(val))
                                {
                                    if (Serializer.IsNullableType(pi.PropertyType))
                                    {
                                        objEnum = Enum.ToObject(Nullable.GetUnderlyingType(pi.PropertyType), int.Parse(val));
                                    }
                                    else
                                    {
                                        objEnum = Enum.ToObject(pi.PropertyType, int.Parse(val));
                                    }
                                }

                                pi.SetValue(obj, objEnum, null);
                            }
                            else
                            {
                                // it's a class
                                typeParameters = pi.PropertyType.GetGenericArguments();

                                if (typeParameters.Length > 0)
                                {
                                    generic = toXMLDocMethod.MakeGenericMethod(typeParameters);
                                }
                                else
                                {
                                    generic = toXMLDocMethod.MakeGenericMethod(pi.PropertyType);
                                }

                                // not primitive type
                                thisObj = null;

                                if (!string.IsNullOrEmpty(valXml))
                                {
                                    thisObj = null;
                                    thisObj = generic.Invoke(null, new object[] { valXml });
                                }
                                pi.SetValue(obj, thisObj, null);
                            }

                            break;
                    }

                }

            }

            return obj;
        }

        /// <summary> Recursive method to serialize an object and its children to an XML string </summary>
        /// <typeparam name="ObjType"></typeparam>
        /// <param name="tObj"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static string toXMLDoc<ObjType>(ObjType tObj, string propertyName) where ObjType : class
        {
            try
            {
                // we get a reference to that method for the recursive calls
                MethodInfo toXMLDocMethod = typeof(Serializer).GetMethod("toXMLDoc");

                System.Reflection.MemberInfo inf = typeof(ObjType);

                StringBuilder builder = new StringBuilder();

                Type type = tObj.GetType();

                if (propertyName == null)
                {
                    builder.Append(string.Format("<{0} Ns=\"{1}\">", inf.Name, type.FullName));
                }
                else
                {
                    builder.Append(string.Format("<{0} Ns=\"{1}\">", propertyName, type.FullName));
                }

                PropertyInfo[] pis = type.GetProperties();

                foreach (PropertyInfo pi in pis)
                {
                    // get members that has a DataMember attribute
                    DataMemberAttribute[] mAttr = pi.GetCustomAttributes(typeof(DataMemberAttribute), true) as DataMemberAttribute[];

                    if (mAttr.Length > 0)
                    {
                        string name = pi.Name;

                        object val = pi.GetValue(tObj, null);

                        // for primitive types + string
                        if (Serializer.IsPrimitive(pi.PropertyType))
                        {
                            if (val != null)
                            {
                                string valAsStr = string.Empty;
                                string baseType = Serializer.GetBaseTypeName(pi.PropertyType);

                                if(pi.PropertyType == typeof(string))
                                {
                                    // it's a string, we escape special characters
                                    valAsStr = Serializer.XMLEncode(val.ToString());
                                }
                                else if (baseType == "System.Enum")
                                {
                                    valAsStr = ((int)val).ToString();
                                }
                                else
                                {
                                    valAsStr = val.ToString();
                                }

                                builder.Append(string.Format("<{0} Ns=\"{1}\">{2}</{0}>", name, Serializer.GetTypeName(pi.PropertyType), valAsStr));
                            }
                            else
                            {
                                builder.Append(string.Format("<{0} Ns=\"{1}\" />", name, Serializer.GetTypeName(pi.PropertyType)));
                            }
                        }
                        // It's a list
                        else if (pi.PropertyType.IsGenericType && pi.PropertyType.FullName.Contains("System.Collections.Generic.List"))
                        {
                            Type[] typeParameters = pi.PropertyType.GetGenericArguments();
                            MethodInfo generic = toXMLDocMethod.MakeGenericMethod(typeParameters);
   
                            // we cast the val to a List
                            System.Collections.IList list = (System.Collections.IList)val;

                            builder.Append(string.Format("<{0} Ns=\"{1}\">", name, "System.Collections.Generic.List"));

                            // recursive call to serialize children
                            foreach (Object item in list)
                            {
                                builder.Append(generic.Invoke(null, new object[] { item, null }) as string);                                
                            }

                            builder.Append(string.Format("</{0}>", name));

                        }
                        // it's a dictionary
                        else if (pi.PropertyType.IsGenericType && pi.PropertyType.FullName.Contains("System.Collections.Generic.Dictionary"))
                        {
                            Type[] typeParameters = pi.PropertyType.GetGenericArguments();
                            MethodInfo generic = toXMLDocMethod.MakeGenericMethod(typeParameters[1]);

                            // we cast the val to a List
                            System.Collections.IDictionary dict  = (System.Collections.IDictionary)val;

                            builder.Append(string.Format("<{0} Ns=\"{1}\">", name, "System.Collections.Generic.Dictionary"));

                            // recursive call to serialize children
                            foreach (Object item in dict.Values)
                            {
                                builder.Append(generic.Invoke(null, new object[] { item, pi.Name }) as string);
                            }

                            builder.Append(string.Format("</{0}>", name));

                        }
                        // it's a plain old object
                        else
                        {
                            if (val != null)
                            {
                                MethodInfo generic = toXMLDocMethod.MakeGenericMethod(pi.PropertyType);
                                builder.Append(generic.Invoke(null, new object[] { val, pi.Name }) as string);
                            }
                            else
                            {
                                builder.Append(string.Format("<{0} Ns=\"{1}\"/>", name, Serializer.GetTypeName(pi.PropertyType)));
                            }
                        }
                    }
                }

                if (propertyName == null)
                {
                    builder.Append(string.Format("</{0}>", inf.Name));
                }
                else
                {
                    builder.Append(string.Format("</{0}>", propertyName));
                }

                return builder.ToString();
            }

            catch
            {
                return string.Empty;
            }
        }

        /// <summary> Returns true if the type passed as argument is primitive. Works with nullable types </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static bool IsPrimitive(Type t)
        {
            // it's a value type, we dont go further
            if (t.IsValueType)
                return true;

            // for nullable types
            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // we get the underlying type
                t = Nullable.GetUnderlyingType(t);
            }
            else
            {
                // we test the type
                if (t == typeof(int) || t == typeof(System.Int32) || t == typeof(System.Int64) ||
                    t == typeof(decimal) || t == typeof(float) || t == typeof(string))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary> Returns the type name or the underlying type if it's nullable </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static string GetTypeName(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // we get the underlying type
                t = Nullable.GetUnderlyingType(t);
            }

            return t.FullName;
        }

        /// <summary> Returns the base type name. Useful mainly for enum </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static string GetBaseTypeName(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // we get the underlying type
                t = Nullable.GetUnderlyingType(t);
            }

            return t.BaseType.FullName;
        }

        /// <summary> Returns whether this type is nullable </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static bool IsNullableType(Type t)
        {
            return (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
        }
 
        /// <summary> Escapes special characters in strings </summary>
        /// <param name="origin"></param>
        /// <returns></returns>
        private static string XMLEncode(string origin)
        {
            return HttpUtility.UrlEncode(origin);
        }

        #endregion
    }
}

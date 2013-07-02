#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;


#endregion

namespace ArdanStudios.Common
{
	///<summary> Provides Encryption Utilities </summary>
	public static class CryptoProvider
	{
		#region Constants

		/// <summary>Password Used For Key generation</summary>
		private static string crypPassword = "{I360o12a93!b7e7@4M5d1#9S5cd%B81098bc1D6f3c}";
		/// <summary>iterations Used For Key generation</summary>
		private static int iterations = 1024;
		/// <summary>Salt Value Used For Key generation</summary>
		private static byte[] crypSalt = Encoding.ASCII.GetBytes("}G1d2#s80a5f~de98@LeIk43db+m{8b8b}ebac4c8a3eAa7{");



		#endregion

		#region Encrypt Methods
		/// <summary>
		/// this function will take a plaintext as an arg and
		/// returns ciphertext as an O/P
		/// </summary>
        /// <param name="plaintext"></param>
		/// <returns></returns>
		public static string EncryptText(string plaintext)
		{
			Rfc2898DeriveBytes KeyBytes = new Rfc2898DeriveBytes(crypPassword, crypSalt, iterations);
			//The deafault iteration count is 1000
			RijndaelManaged algorithm = new RijndaelManaged();
			algorithm.Key = KeyBytes.GetBytes(32);
			algorithm.IV = KeyBytes.GetBytes(16);
			CryptoStream encrypt;
			byte[] encryptBytes;
			using (MemoryStream encryptStream = new MemoryStream())
			{ //Stream to write
				encrypt = new CryptoStream(encryptStream, algorithm.CreateEncryptor(), CryptoStreamMode.Write);
				//convert plain text to byte array
				byte[] data = Encoding.UTF8.GetBytes(plaintext);
				encrypt.Write(data, 0, data.Length); //data to encrypt,start,stop
				encrypt.FlushFinalBlock();//Clear buffer
				encrypt.Close();
				encrypt.Dispose();
				encryptBytes = encryptStream.ToArray();
			}
			return Convert.ToBase64String(encryptBytes);//return encrypted data
		}


		/// <summary>
		/// this function will take a ciphertext as an arg and
		/// returns plaintext as an O/P
		/// </summary>
		/// <param name="ciphertext"></param>
		/// <returns></returns>
		public static string DecryptText(string ciphertext)
		{
			Rfc2898DeriveBytes keyBytes = new Rfc2898DeriveBytes(crypPassword, crypSalt, iterations);
			//The deafault iteration count is 1000
			RijndaelManaged algorithm = new RijndaelManaged();
			algorithm.Key = keyBytes.GetBytes(32);
			algorithm.IV = keyBytes.GetBytes(16);
			CryptoStream decrypt;
			byte[] decryptedBytes;
			using (MemoryStream decryptStream = new MemoryStream())
			{ //Stream to read
				decrypt = new CryptoStream(decryptStream, algorithm.CreateDecryptor(), CryptoStreamMode.Write);
				//convert  ciphertext to byte array
				byte[] data = Convert.FromBase64String(ciphertext); //IF using for WEB APPLICATION and getting ciphertext via Querystring change code to : Convert.FromBase64String(ciphertext.Replace(” “,”+”));
				decrypt.Write(data, 0, data.Length); //data to encrypt,start,stop
				decrypt.Flush();
				decrypt.Close();
				decrypt.Dispose();
				decryptedBytes = decryptStream.ToArray();
			}
			return Encoding.UTF8.GetString(decryptedBytes);//return PlainText
		}
		#endregion
	}
}






























using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class PortableToken {
 const string Prefix="aesgcm1:";
 public static string Protect(string value,string directory) {
  Directory.CreateDirectory(directory);string path=Path.Combine(directory,"token.key");
  if(!File.Exists(path)) {using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(RandomNumberGenerator.GetBytes(32));stream.Flush(true);}
  byte[] key=File.ReadAllBytes(path),nonce=RandomNumberGenerator.GetBytes(12),data=Encoding.UTF8.GetBytes(value),cipher=new byte[data.Length],tag=new byte[16];
  using var aes=new AesGcm(key,16);aes.Encrypt(nonce,data,cipher,tag);
  return Prefix+Convert.ToBase64String(nonce)+":"+Convert.ToBase64String(tag)+":"+Convert.ToBase64String(cipher);
 }
 public static string Read(string value,string directory) {
  if(!value.StartsWith(Prefix))return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value),null,DataProtectionScope.CurrentUser));
  var parts=value.Substring(Prefix.Length).Split(':');if(parts.Length!=3)throw new CryptographicException();
  byte[] key=File.ReadAllBytes(Path.Combine(directory,"token.key")),cipher=Convert.FromBase64String(parts[2]),plain=new byte[cipher.Length];
  using var aes=new AesGcm(key,16);aes.Decrypt(Convert.FromBase64String(parts[0]),cipher,Convert.FromBase64String(parts[1]),plain);return Encoding.UTF8.GetString(plain);
 }
 public static void Migrate(ConnectionSettings settings,string directory) {
  foreach(var account in settings.Accounts)if(!string.IsNullOrEmpty(account.ProtectedToken)&&!account.ProtectedToken.StartsWith(Prefix))account.ProtectedToken=Protect(Read(account.ProtectedToken,directory),directory);
  AccountProfile.Migrate(settings);
 }
 public static void Tests() {
  string root=Path.Combine(Path.GetTempPath(),"KimaiBlocks-token-"+Guid.NewGuid().ToString("N"));
  try {string a=Path.Combine(root,"a"),b=Path.Combine(root,"b");string token=Protect("test-secret",a);Directory.CreateDirectory(b);File.Copy(Path.Combine(a,"token.key"),Path.Combine(b,"token.key"));if(Read(token,b)!="test-secret"||token.Contains("test-secret"))throw new Exception("Portable encryption failed");File.WriteAllBytes(Path.Combine(b,"token.key"),RandomNumberGenerator.GetBytes(32));try {Read(token,b);throw new Exception("Wrong key accepted");}catch(AuthenticationTagMismatchException){}}
  finally {if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}


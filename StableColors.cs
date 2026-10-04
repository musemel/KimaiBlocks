using System;
using System.Linq;
using System.Text;
using System.Windows.Media;

public static class StableColors {
 public static uint Hash(string name){int id=(name??"").LastIndexOf(" [#",StringComparison.Ordinal);if(id>=0)name=name.Substring(0,id);uint hash=2166136261;foreach(byte b in Encoding.UTF8.GetBytes((name??"").Normalize())){hash^=b;hash=unchecked(hash*16777619);}return hash;}
 public static string Color(string name,int palette){uint hash=Hash(name);return Blocks.AutoColor((int)(hash%(palette==16?16u:palette==256?256u:10000000u)));}
 public static string Nearby(string original,int attempt){var c=(System.Windows.Media.Color)ColorConverter.ConvertFromString(original);int offset=(attempt+1)/2*(attempt%2==0?-1:1);return $"#{Math.Clamp(c.R+offset,0,255):X2}{Math.Clamp(c.G-offset/2,0,255):X2}{Math.Clamp(c.B+offset/3,0,255):X2}";}
 public static void Tests(){if(Color("Example [#1]",16)!=Color("Example [#2]",16)||Enumerable.Range(0,2000).Select(i=>Color("Project"+i,16)).Distinct().Count()>16||Enumerable.Range(0,2000).Select(i=>Color("Project"+i,256)).Distinct().Count()>256)throw new Exception("Stable project colors");}
}


using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed class ActivityGrouping {
 public const string DefaultPattern="(.*)/(.*)";
 Regex regex;
 public ActivityGrouping(string pattern) {
  if(string.IsNullOrWhiteSpace(pattern))return;
  regex=new Regex(pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
  if(!regex.GetGroupNumbers().Contains(1)||!regex.GetGroupNumbers().Contains(2))throw new ArgumentException("アクティビティの正規表現には、第1・第2キャプチャグループ（括弧）が必要です。");
 }
 public (string Group,string Name) Split(string activity) {
  if(regex==null)return (null,activity);
  try {
   var match=regex.Match(activity);
   if(match.Success&&match.Groups[1].Success&&match.Groups[2].Success&&match.Groups[1].Value.Length>0&&match.Groups[2].Value.Length>0)return (match.Groups[1].Value,match.Groups[2].Value);
  }catch(RegexMatchTimeoutException){regex=null;}
  return (null,activity);
 }
 public static void Tests() {
  var grouping=new ActivityGrouping(DefaultPattern);
  if(grouping.Split("aaa/xxx")!=("aaa","xxx")||grouping.Split("aaa/yyy")!=("aaa","yyy")||grouping.Split("aaa/bbb/xxx")!=("aaa/bbb","xxx"))throw new Exception("Regex activity grouping");
  if(grouping.Split("plain")!=(null,"plain")||grouping.Split("aaa/")!=(null,"aaa/")||new ActivityGrouping("").Split("aaa/xxx")!=(null,"aaa/xxx")||new ActivityGrouping(@"^\[(.*?)\] (.*)$").Split("[team] task")!=("team","task"))throw new Exception("Regex grouping fallback/custom pattern");
  foreach(var pattern in new[]{"(","(.*)"}){bool rejected=false;try{new ActivityGrouping(pattern);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid grouping pattern accepted");}
 }
}


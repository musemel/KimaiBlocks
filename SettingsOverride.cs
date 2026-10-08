using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;

public static class SettingsLayers {
 public static readonly JsonSerializerOptions Options=new JsonSerializerOptions {PropertyNameCaseInsensitive=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,WriteIndented=true};
 public static JsonObject Object(string json)=>JsonNode.Parse(json,new JsonNodeOptions {PropertyNameCaseInsensitive=true},new JsonDocumentOptions {CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true}) as JsonObject??throw new ArgumentException("設定はJSONオブジェクトで指定してください。");
 public static void Merge(JsonObject target,JsonObject values) {foreach(var p in values){string key=target.Select(x=>x.Key).FirstOrDefault(k=>string.Equals(k,p.Key,StringComparison.OrdinalIgnoreCase))??p.Key;if(p.Value is JsonObject child&&target[key] is JsonObject old)Merge(old,child);else target[key]=p.Value?.DeepClone();}}
 public static ConnectionSettings Load(string defaults,string saved=null,JsonObject forced=null) {
  var data=JsonSerializer.SerializeToNode(new ConnectionSettings()).AsObject();if(defaults!=null)Merge(data,Object(defaults));var initial=data.DeepClone().AsObject();if(saved!=null)Merge(data,Object(saved));if(forced!=null)Merge(data,forced);if(data["Accounts"] is JsonArray accounts){foreach(var node in accounts.OfType<JsonObject>()){var account=new JsonObject();foreach(string key in new[]{"Url","Username","Legacy","AllowHttp"})account[key]=initial[key]?.DeepClone();Merge(account,node);foreach(var pair in account.ToArray())node[pair.Key]=pair.Value?.DeepClone();}}return Validate(data.Deserialize<ConnectionSettings>(Options));
 }
 public static ConnectionSettings Validate(ConnectionSettings value) {
  if(value.Calendar==null||value.Accounts==null||value.ExcludedReportUserIds==null||value.PeriodReport==null)throw new ArgumentException("Calendar / Accounts / ExcludedReportUserIds / PeriodReport にnullは指定できません。");
  if(value.Calendar.DaysOff==null||value.Calendar.DaysOff.Any(d=>d<0||d>6)||value.ExcludedReportUserIds.Any(id=>id<=0))throw new ArgumentException("曜日は0〜6、除外ユーザーIDは正の整数で指定してください。");value.PeriodReport.Validate();value.Calendar.Intervals();value.Calendar.HolidayDates();_ = new ActivityGrouping(value.ActivityGroupingPattern);
  if(value.SaveSeconds<10||value.SaveSeconds>3600||value.CatalogMinutes<1||value.CatalogMinutes>1440||!new[]{0,16,256}.Contains(value.ProjectPalette)||value.ZoomPercent<50||value.ZoomPercent>400||value.ReportMinimumHours<0||value.ReportMinimumHours>24)throw new ArgumentException("設定値が許容範囲外です。保存間隔10〜3600秒、キャッシュ1〜1440分、倍率50〜400%、基準時間0〜24時間で指定してください。");
  return value;
 }
}
public sealed class ManagedSettings {
 public JsonObject Values {get;private set;}=new JsonObject();
 public string UpdateFolder=>Values.FirstOrDefault(p=>p.Key.Equals("UpdateFolder",StringComparison.OrdinalIgnoreCase)).Value?.GetValue<string>();
 public string ActivityGroupingPattern=>Values.FirstOrDefault(p=>p.Key.Equals("ActivityGroupingPattern",StringComparison.OrdinalIgnoreCase)).Value?.GetValue<string>();
 public bool Locked(string path){JsonNode node=Values;var parts=path.Split('.');for(int i=0;i<parts.Length;i++){if(node is not JsonObject obj)return true;var key=obj.Select(p=>p.Key).FirstOrDefault(k=>k.Equals(parts[i],StringComparison.OrdinalIgnoreCase));if(key==null)return false;node=obj[key];}return node is not JsonObject nested||nested.Count>0;}
 public static ManagedSettings Read(string json){var value=new ManagedSettings {Values=SettingsLayers.Object(json)};value.Apply(new ConnectionSettings());return value;}
 public ConnectionSettings Apply(ConnectionSettings settings){var data=JsonSerializer.SerializeToNode(settings).AsObject();SettingsLayers.Merge(data,Values);var result=SettingsLayers.Validate(data.Deserialize<ConnectionSettings>(SettingsLayers.Options));if(result.Url!=settings.Url&&!Locked("UserId"))result.UserId=0;foreach(var account in result.Accounts)ApplyAccount(account);if(Locked("ActiveAccountId")&&result.Accounts.Count>0){var active=result.Accounts.FirstOrDefault(a=>a.Id==result.ActiveAccountId)??throw new ArgumentException("強制指定したActiveAccountIdがアカウント一覧にありません。");AccountProfile.Select(result,active);}return result;}
 public void ApplyAccount(AccountProfile account){string oldUrl=account.Url;foreach(var prop in typeof(AccountProfile).GetProperties().Where(p=>p.CanWrite)){var key=Values.Select(p=>p.Key).FirstOrDefault(k=>k.Equals(prop.Name,StringComparison.OrdinalIgnoreCase));if(key!=null)prop.SetValue(account,Values[key]?.Deserialize(prop.PropertyType,SettingsLayers.Options));}if(account.Url!=oldUrl&&!Locked("UserId"))account.UserId=0;}
 public static void Tests(){var defaults="{\"Url\":\"https://default.test\",\"SaveSeconds\":90,\"Calendar\":{\"BlockInput\":true}}";var saved="{\"SaveSeconds\":30}";var s=SettingsLayers.Load(defaults,saved);if(s.SaveSeconds!=30||s.Url!="https://default.test"||!s.Calendar.BlockInput)throw new Exception("Defaults / saved precedence");var policy=Read("{\"url\":\"https://forced.test\",\"ActivityGroupingPattern\":\"(.*)/(.*)\",\"Calendar\":{\"BlockInput\":false},\"SaveSeconds\":20}");s.Accounts.Add(new AccountProfile {Url="https://personal.test"});s=policy.Apply(s);AccountProfile.Select(s,s.Accounts[0]);if(s.Url!="https://forced.test"||s.Calendar.BlockInput||s.SaveSeconds!=20||!policy.Locked("Calendar.BlockInput")||policy.Locked("Calendar.Breaks")||!policy.Locked("Url"))throw new Exception("Override precedence / locks");bool failed=false;try{Read("{\"Unknown\":20}");}catch(JsonException){failed=true;}if(!failed)throw new Exception("Unknown settings accepted");}
}
public partial class Blocks {
 ManagedSettings managedSettings=new ManagedSettings();
 string defaultsJson;
 string EffectiveUpdateFolder=>settings.UpdateFolder;
 string EffectiveActivityPattern=>settings.ActivityGroupingPattern;
 bool Locked(string key)=>managedSettings.Locked(key);
 readonly System.Collections.Generic.List<(FrameworkElement Control,string Key)> policyControls=new System.Collections.Generic.List<(FrameworkElement,string)>();
 void LockSetting(FrameworkElement control,string key){if(!policyControls.Any(x=>x.Control==control))policyControls.Add((control,key));if(Locked(key)){control.IsEnabled=false;control.ToolTip="settings.override.json で指定されているため変更できません。";}}
 void ApplyManagedSettings(){settings=managedSettings.Apply(settings);foreach(var item in policyControls.ToArray())LockSetting(item.Control,item.Key);}
 AccountProfile NewDefaultAccount(){var initial=SettingsLayers.Load(defaultsJson);var account=new AccountProfile {Url=initial.Url,Username=initial.Username,Legacy=initial.Legacy,AllowHttp=initial.AllowHttp};managedSettings.ApplyAccount(account);return account;}
 void ApplyStartupOverrides(string directory=null) {
  var values=new JsonObject();foreach(string name in new[]{"settings.policy.json","settings.override.json"}){string path=Path.Combine(directory??AppContext.BaseDirectory,name);if(File.Exists(path)){try{SettingsLayers.Merge(values,SettingsLayers.Object(File.ReadAllText(path)));}catch(Exception ex){throw new ArgumentException(name+" を読み込めません: "+ex.Message);}}}
  try{managedSettings=ManagedSettings.Read(values.ToJsonString());ApplyManagedSettings();}catch(Exception ex){throw new ArgumentException("強制設定を適用できません: "+ex.Message);}
 }
}

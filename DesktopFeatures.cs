using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

public sealed class DistributionDefaults {
 public string ActivityGroupingPattern {get;set;}=ActivityGrouping.DefaultPattern;
 public CalendarRules Calendar {get;set;}=new CalendarRules();
 public string Url {get;set;}="";
 public int SaveSeconds {get;set;}=60;
 public int CatalogMinutes {get;set;}=60;
 public string UpdateFolder {get;set;}="";
}
public partial class Blocks {
 static string AppVersion=>Assembly.GetExecutingAssembly().GetName().Version.ToString();
 readonly DispatcherTimer clockTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(30)};
 Border nowLine;TextBlock nowLabel;
 void StartClock(){clockTimer.Tick+=(s,e)=>DrawNow();clockTimer.Start();}
 void DrawNow() {
  if(nowLine!=null)board.Children.Remove(nowLine);if(nowLabel!=null)board.Children.Remove(nowLabel);
  DateTime now=DateTime.Now;
  try {if(!string.IsNullOrEmpty(service?.Me?.Timezone))now=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(service.Me.Timezone));}catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
  int day=(now.Date-DisplayStart).Days;if(day<0||day>=DisplayDayCount)return;
  double y=now.TimeOfDay.TotalHours*Hour;
  nowLine=new Border {Height=2,Width=DayWidth,Background=Brushes.Crimson,IsHitTestVisible=false};Canvas.SetLeft(nowLine,Gutter+day*DayWidth);Canvas.SetTop(nowLine,y);Panel.SetZIndex(nowLine,1000);board.Children.Add(nowLine);
  nowLabel=new TextBlock {Text=now.ToString("HH:mm"),Foreground=Brushes.White,Background=Brushes.Crimson,FontSize=11,Padding=new Thickness(2),IsHitTestVisible=false};Canvas.SetLeft(nowLabel,Gutter+day*DayWidth);Canvas.SetTop(nowLabel,Math.Max(0,y-18));Panel.SetZIndex(nowLabel,1001);board.Children.Add(nowLabel);
 }
 void LoadDefaults() {
  string path=Path.Combine(AppContext.BaseDirectory,"defaults.json");if(!File.Exists(path))return;
  var value=JsonSerializer.Deserialize<DistributionDefaults>(File.ReadAllText(path),new JsonSerializerOptions {PropertyNameCaseInsensitive=true})??new DistributionDefaults();
  var calendar=value.Calendar??new CalendarRules();calendar.Intervals();calendar.HolidayDates();settings.Calendar=calendar;
  _ = new ActivityGrouping(value.ActivityGroupingPattern);settings.ActivityGroupingPattern=value.ActivityGroupingPattern;
  settings.Url=value.Url??"";settings.SaveSeconds=Math.Clamp(value.SaveSeconds,10,3600);settings.CatalogMinutes=Math.Clamp(value.CatalogMinutes,1,1440);settings.UpdateFolder=value.UpdateFolder??"";
 }
 bool checkingUpdates;
 async Task CheckUpdates(bool manual) {
  if(checkingUpdates)return;
  if(string.IsNullOrWhiteSpace(settings.UpdateFolder)){if(manual)MessageBox.Show(this,"設定でアップデート確認フォルダを指定してください。","アップデート");return;}
  checkingUpdates=true;string folder=settings.UpdateFolder;
  try {
   var read=Task.Run(()=>{try {return (Version:UpdateVersion.Read(folder),Error:(string)null);}catch(Exception){return (Version:(Version)null,Error:"確認先のKimaiBlocks.dll または KimaiBlocks.exe のバージョンを読み込めません。パスとアクセス権を確認してください。");}});
   if(await Task.WhenAny(read,Task.Delay(TimeSpan.FromSeconds(8)))!=read){if(manual)MessageBox.Show(this,"確認がタイムアウトしました。","アップデート");return;}
   var result=await read;
   if(!IsVisible)return;
   if(result.Error!=null){if(manual)MessageBox.Show(this,result.Error,"アップデート");return;}
   if(result.Version>Version.Parse(AppVersion)){try{StageUpdateOverride(folder,result.Version);}catch(Exception ex){MessageBox.Show(this,SafeError(ex),"更新用の上書き設定をコピーできません");}MessageBox.Show(this,"新しいバージョンがあります: "+result.Version+"\n現在: "+AppVersion+"\n配布フォルダ: "+folder+"\n保存して終了後、配布フォルダの一式をコピーして更新してください。\n配布元のsettings.override.jsonがあれば取り込み、更新後の初回起動時に適用します。","アップデートのお知らせ");}
   else if(manual)MessageBox.Show(this,"確認先に新しいバージョンはありません。現在: "+AppVersion,"アップデート");
  }finally {checkingUpdates=false;}
 }
 // Restore only unsaved local changes. Requests already accepted by Kimai are never undone here.
 internal static void RollbackPending(State data) {
  foreach(var p in data.Pending) {
   data.Entries.RemoveAll(e=>e.LocalKey==p.Desired.LocalKey);
   if(p.Original!=null){var restored=p.Original.Copy();restored.LocalKey=p.Desired.LocalKey;data.Entries.Add(restored);}
  }
  data.Pending.Clear();
 }
 bool OfferDiscard(string message) {
  if(testError!=null){testError(message);return false;}
  var w=new Window {Title="保存失敗",Owner=progressWindow??this,Width=560,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel {Margin=new Thickness(20)};w.Content=panel;var text=Label(message+"\n未保存変更を破棄するとローカル編集をキャンセルします。送信済みの変更はサーバーに残る場合があります。",13);text.TextWrapping=TextWrapping.Wrap;panel.Children.Add(text);
  bool discard=false;panel.Children.Add(ButtonOf("変更を保持して戻る",()=>w.Close()));panel.Children.Add(ButtonOf("未保存変更を破棄…",()=>{if(MessageBox.Show(w,"残っている未保存変更をすべて破棄しますか？この操作は取り消せません。","変更の破棄",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes){discard=true;w.Close();}}));w.ShowDialog();
  if(!discard)return false;
  var oldEntries=state.Entries.ToList();var oldPending=state.Pending.ToList();bool committed=false;
  try {RollbackPending(state);Persist();ClearHistory();committed=true;needsRefresh=true;savePaused=true;if(file!=null)File.Delete(file+".bak");Render();status.Text="未保存変更を破棄しました。再読込してください。";return true;}
  catch(Exception ex){if(!committed){state.Entries=oldEntries;state.Pending=oldPending;}MessageBox.Show(progressWindow??this,SafeError(ex),committed?"変更は破棄済みですがバックアップを削除できません":"変更の破棄を保存できません");return false;}
 }
 async Task ResetFromServer() {
  if(communicating)return;
  if(MessageBox.Show(this,"現在のアカウントの未保存変更と実績・一覧キャッシュのバックアップを破棄し、表示週をサーバーから取得します。\n表示選択・フォルダ・接続設定は保持します。\n送信済みの変更はサーバーに残ります。続けますか？","サーバーから再取得",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  await BeginProgress("キャッシュを使用せずサーバーから再取得しています…");KimaiService candidate=null;
  var oldEntries=state.Entries;var oldPending=state.Pending;var oldProjects=Projects;var oldHidden=state.Hidden.ToList();var oldCollapsed=state.Collapsed.ToList();string oldFile=file;bool committed=false;
  try {
   candidate=new KimaiService(settings.Url,PortableToken.Read(settings.ProtectedToken,DataDirectory),settings.Username,settings.Legacy,allowHttp:settings.AllowHttp);await candidate.InitializeAsync();
   string target=AccountFile(candidate.BaseUrl,candidate.Me.Id.Value);
   if(file!=null&&file!=target)throw new KimaiFailure("接続ユーザーが変わっています。設定からアカウントを切り替えてください。");
   var entries=await candidate.ReadWeekAsync(week);
   state.Entries=entries;state.Pending=new System.Collections.Generic.List<PendingChange>();file=target;
   var previous=service;service=candidate;candidate=null;
   try {await RefreshView();Persist();committed=true;}catch {candidate=service;service=previous;throw;}
   ClearHistory();previous?.Dispose();File.Delete(file+".bak");File.Delete(file+".catalog");needsRefresh=false;savePaused=false;ConfigureTimer();selected=null;status.Text="サーバーから再取得しました";Render();
  }catch(Exception ex){if(!committed){file=oldFile;state.Entries=oldEntries;state.Pending=oldPending;Projects=oldProjects;state.Hidden=oldHidden;state.Collapsed=oldCollapsed;}needsRefresh=true;MessageBox.Show(progressWindow,SafeError(ex),"再取得失敗");}
  finally {candidate?.Dispose();EndProgress();Populate();PopulateProjectList();Render();}
 }
}





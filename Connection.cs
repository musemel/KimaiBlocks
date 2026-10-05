using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

[DataContract] public sealed class PendingChange {
 [DataMember] public Entry Desired;
 [DataMember] public Entry Original;
 [DataMember] public bool Delete;
 [DataMember] public bool Attempted;
}
public sealed class ConnectionSettings {
 public int ProjectPalette {get;set;}=16;
 public bool AvoidColorCollisions {get;set;}=false;
 public string ActivityGroupingPattern {get;set;}=ActivityGrouping.DefaultPattern;
 public List<AccountProfile> Accounts {get;set;}=new List<AccountProfile>();
 public string ActiveAccountId {get;set;}="";
 public string Url {get;set;}="";
 public string Username {get;set;}="";
 public bool Legacy {get;set;}
 public bool AllowHttp {get;set;}
 public string ProtectedToken {get;set;}="";
 public int SaveSeconds {get;set;}=60;
 public int CatalogMinutes {get;set;}=60;
 public CalendarRules Calendar {get;set;}=new CalendarRules();
 public int ZoomPercent {get;set;}=100;
 public double LeftPanelWidth {get;set;}=280;
 public double RightPanelWidth {get;set;}=320;
 public bool ShowWeekends {get;set;}=false;
 public bool ShowStatistics {get;set;}=true;
 public string UpdateFolder {get;set;}="";
 public int UserId {get;set;}
}
public partial class Blocks {
 KimaiService service;
 bool communicating,needsRefresh,closingApproved,closingRequested,savePaused,demoMode;
 ConnectionSettings settings=new ConnectionSettings();
 DispatcherTimer saveTimer=new DispatcherTimer();
 TextBlock connectionBadge=new TextBlock();
 Action<string> testError;
  Window progressWindow;
 TextBlock progressText;
 static string DataDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KimaiBlocks");
 static string SettingsFile=>Path.Combine(DataDirectory,"connection.json");
 static string AccountFile(string url,int user)=>Path.Combine(DataDirectory,"kimai-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url+"\n"+user))).Substring(0,24)+".json");
 static string SafeError(Exception ex)=>ex is KimaiFailure||ex is ArgumentException?ex.Message:"通信またはファイル処理に失敗しました。接続設定と保存先を確認してください。";
 void AddConnectionTools(DockPanel top,bool demo) {
  demoMode=demo;
  var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(8)};
  var menu=new Menu {Background=Brushes.Transparent,VerticalAlignment=VerticalAlignment.Center};
  var item=new MenuItem {Header="メニュー",Foreground=Brushes.White};
  MenuItem Group(string title){var group=new MenuItem {Header=title,Foreground=Brushes.Black};item.Items.Add(group);return group;}
  void ActionItem(MenuItem parent,string title,Action action){var entry=new MenuItem {Header=title};entry.Click+=(s,e)=>action();parent.Items.Add(entry);}
  var connectionMenu=Group("接続・データ");
  ActionItem(connectionMenu,"アカウント・接続設定…",ConnectionDialog);
  ActionItem(connectionMenu,"再読込",async()=>await RefreshRemote());
  ActionItem(connectionMenu,"今すぐ保存",async()=>await FlushAsync(true));
  ActionItem(connectionMenu,"バックアップを破棄してサーバーから再取得…",async()=>await ResetFromServer());
  var displayMenu=Group("表示・作業ツリー");
  ActionItem(displayMenu,"表示プロジェクト…",ShowProjectChoices);
  ActionItem(displayMenu,"表示・分類設定…",ViewSettingsDialog);
  ActionItem(displayMenu,"プロジェクトの固定色…",()=>ColorDialog());


  var zoomMenu=new MenuItem {Header="表示倍率"};displayMenu.Items.Add(zoomMenu);foreach(int percent in new[]{50,75,100,125,150,175,200,250,300,400}){int value=percent;ActionItem(zoomMenu,percent+"%",()=>SetZoom(value));}
  ActionItem(displayMenu,"右の統計パネルを表示／非表示",()=>SetStatisticsVisible(!settings.ShowStatistics));
  var workMenu=Group("作業・カレンダー");
  ActionItem(workMenu,"プロジェクト追加…",async()=>await ProjectDialog());
  ActionItem(workMenu,"休日・休み時間・時間外…",CalendarDialog);
  ActionItem(workMenu,"時間外・休み時間の入力ロック切り替え",ToggleInputLock);
  var reportMenu=Group("集計");ActionItem(reportMenu,"コメント別の詳細集計…",ShowDetailedStatistics);ActionItem(reportMenu,"サーバー実績集計（全ユーザー）…",ShowServerReports);
  var maintenance=Group("更新・配布設定");ActionItem(maintenance,"アップデートを確認",async()=>await CheckUpdates(true));
  ActionItem(maintenance,"自動保存・キャッシュ・更新先…",SyncSettingsDialog);
  menu.Items.Add(item);rightClock=Label("",15);rightClock.Foreground=BrushOf("#D9E6F1");rightClock.Margin=new Thickness(10,0,18,0);buttons.Children.Add(rightClock);buttons.Children.Add(menu);

  DockPanel.SetDock(buttons,Dock.Right);top.Children.Insert(0,buttons);
  connectionBadge.Text="Kimai未接続";connectionBadge.Foreground=BrushOf("#C7D7E6");connectionBadge.VerticalAlignment=VerticalAlignment.Center;top.Children.Add(connectionBadge);
  saveTimer.Tick+=async(s,e)=>{if(inlineComment==null&&!editorDirty&&!dragActive&&!communicating&&!savePaused&&!closingRequested&&OwnedWindows.Count==0&&state.Pending.Count>0)await FlushAsync(false);};
  Loaded+=async(s,e)=>{if(!demo)await StartupAsync();};
  Closing+=async(s,e)=>{
   if(demoMode||closingApproved)return;e.Cancel=true;
   if(communicating){MessageBox.Show(this,"読み込み・保存が完了するまで終了できません。","終了待機");return;}
   if(closingRequested)return;closingRequested=true;
   try {if(!ApplyEditor())return;if(!await FlushAsync(true))return;Persist();closingApproved=true;_ = Dispatcher.BeginInvoke(new Action(Close));}
   catch(Exception ex){MessageBox.Show(this,SafeError(ex)+"\n保存できないため終了を中止しました。","終了できません");}
   finally {closingRequested=false;}
  };
  StartClock();
  Closed+=(s,e)=>{clockTimer.Stop();saveTimer.Stop();service?.Dispose();};
 }
 async Task StartupAsync() {
  try {
   if(!File.Exists(SettingsFile)){LoadDefaults();ApplyStartupOverrides();RestoreViewPreferences();ConnectionDialog();_ = CheckUpdates(false);return;}
   settings=JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(SettingsFile))??new ConnectionSettings();
   ApplyStartupOverrides();
   RestoreViewPreferences();AccountProfile.Migrate(settings);PortableToken.Migrate(settings,DataDirectory);StoreSettings();_ = CheckUpdates(false);
   if(settings.UserId>0)LoadAccount(settings.Url,settings.UserId);
   await ConnectAsync();
  }catch(Exception ex){needsRefresh=true;MessageBox.Show(this,SafeError(ex),"起動時の読み込み失敗");}
 }
 void SetCommunicating(bool value) {communicating=value;((UIElement)Content).IsEnabled=!value;}
 async Task BeginProgress(string text) {
  SetCommunicating(true);
  progressText=Label(text,14);
  var panel=new StackPanel {Margin=new Thickness(24)};panel.Children.Add(progressText);panel.Children.Add(new ProgressBar {IsIndeterminate=true,Height=12,Margin=new Thickness(4,16,4,4)});
  progressWindow=new Window {Title="Kimai Blocks",Owner=this,Width=420,Height=160,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
  progressWindow.Closing+=(s,e)=>{if(communicating)e.Cancel=true;};progressWindow.Show();
  await Dispatcher.Yield(DispatcherPriority.Background);
 }
 void EndProgress() {SetCommunicating(false);progressWindow?.Close();progressWindow=null;}
 void ConfigureTimer() {saveTimer.Interval=TimeSpan.FromSeconds(Math.Clamp(settings.SaveSeconds,10,3600));saveTimer.Start();}
 void LoadAccount(string url,int user) {
  string account=AccountFile(url,user);if(file==account)return;
  State loaded=new State();
  if(File.Exists(account)){using(var stream=File.OpenRead(account))loaded=(State)new DataContractJsonSerializer(typeof(State)).ReadObject(stream);}
  if(loaded?.Entries==null||loaded.Hidden==null||loaded.Favorites==null)throw new IOException("Invalid cache");
  clipboardEntries.Clear();ClearHistory();state=loaded;file=account;Projects=state.CachedProjects;
  // Pending desired values take precedence over the last downloaded snapshot.
  foreach(var change in state.Pending.Where(p=>!p.Delete)) {
   var existing=state.Entries.FindIndex(e=>e.LocalKey!=null&&e.LocalKey==change.Desired.LocalKey);
   if(existing>=0)state.Entries[existing]=change.Desired;else state.Entries.Add(change.Desired);
  }
  if(state.CachedWeek!=default)week=state.CachedWeek;customFrom=state.CachedFrom;customThrough=state.CachedThrough;if(!customFrom.HasValue||!customThrough.HasValue||!ValidDisplayRange(customFrom.Value,customThrough.Value)){customFrom=null;customThrough=null;}rangeDayWidth=state.CachedDayWidth>=100?Math.Clamp(state.CachedDayWidth,100,400):180;RefreshReuse();
  Render();status.Text="キャッシュ表示（接続確認中）";
 }
 async Task ConnectAsync() {
  if(communicating)return;
  await BeginProgress("Kimaiに接続しています…");KimaiService candidate=null;
  try {
   string token=PortableToken.Read(settings.ProtectedToken,DataDirectory);
   candidate=new KimaiService(settings.Url,token,settings.Username,settings.Legacy,allowHttp:settings.AllowHttp);
   await candidate.InitializeAsync(false);
   if(state.Pending.Count>0&&file!=AccountFile(candidate.BaseUrl,candidate.Me.Id.Value))throw new KimaiFailure("未保存の変更があるため接続先を変更できません。");
   LoadAccount(candidate.BaseUrl,candidate.Me.Id.Value);
   progressText.Text="プロジェクトとアクティビティを読み込み中…";
   await LoadCatalog(candidate,false);
   service?.Dispose();service=candidate;candidate=null;
   settings.UserId=service.Me.Id.Value;var active=settings.Accounts.FirstOrDefault(a=>a.Id==settings.ActiveAccountId);if(active!=null)active.UserId=settings.UserId;StoreSettings();ConfigureTimer();
   connectionBadge.Text="接続: "+(settings.Accounts.FirstOrDefault(a=>a.Id==settings.ActiveAccountId)?.Name??service.Me.Username)+" / "+service.Me.Username+" · "+service.Me.Timezone;
   needsRefresh=false;savePaused=state.Pending.Any(p=>p.Attempted);
   await RefreshView();
   if(state.Pending.Count==0){ClearHistory();progressText.Text="今週の実績を読み込み中…";var current=Monday(DateTime.Today);var entries=await service.ReadWeekAsync(current);week=current;customFrom=null;customThrough=null;state.Entries=entries;SeedInputHistory(entries);RefreshReuse();}
   Render();Persist();status.Text=state.Pending.Count>0?"未保存の変更をキャッシュから復元しました":"Kimai読込済み";
   if(savePaused)MessageBox.Show(progressWindow,"送信途中の変更を復元しました。「再読込」で保存結果を確認してください。","保存結果の確認が必要です");
  }catch(Exception ex){needsRefresh=true;savePaused=true;MessageBox.Show(progressWindow,SafeError(ex),"接続・読み込み失敗");}
  finally {candidate?.Dispose();EndProgress();}
 }
 async Task RefreshView() {
  var old=Projects;Projects=service.Projects.Where(p=>p.Visible!=false).Select(p=>service.ProjectName(p.Id.Value)).ToArray();
  foreach(var p in Projects.Except(old)){if(!state.Hidden.Contains(p))state.Hidden.Add(p);if(!state.Collapsed.Contains("project:"+p))state.Collapsed.Add("project:"+p);}
  await Dispatcher.Yield(DispatcherPriority.Background);PopulateProjectList();Populate();Render();
 }
 bool CanEdit(Entry en=null) {
  if(demoMode)return true;
  if(communicating||service==null||needsRefresh){status.Text="接続・再読込を完了してから編集してください。";return false;}
  if(state.Pending.Any(p=>p.Attempted)){MessageBox.Show(this,"保存結果が未確認です。「再読込」で確認してください。");return false;}
  if(en?.ReadOnlyReason!=null){MessageBox.Show(this,"読み取り専用: "+en.ReadOnlyReason);return false;}return true;
 }
 Task CommitEntry(Entry en,Entry before) {
  EditingModel.Key(en);if(before!=null)before.LocalKey=en.LocalKey;
  var desired=en.Copy();if(before!=null)RestoreEntry(en,before);else state.Entries.Remove(en);
  bool timing=before==null||before.Start!=desired.Start||before.Minutes!=desired.Minutes;
  if(timing) {
   var blockers=state.Entries.Where(e=>!ReferenceEquals(e,en));var parts=EditingModel.Plan(desired,blockers,Rules);
   if(parts.Count==0){if(before==null)selected=null;Render();status.Text="入力可能な空き時間がないため変更しませんでした。";return Task.CompletedTask;}
   Remember();
   if(before!=null){RestoreEntry(en,parts[0]);PendingQueue.Edit(state.Pending,en,before);foreach(var part in parts.Skip(1)){state.Entries.Add(part);PendingQueue.Edit(state.Pending,part,null);}}
   else {RestoreEntry(en,parts[0]);state.Entries.Add(en);PendingQueue.Edit(state.Pending,en,null);foreach(var part in parts.Skip(1)){state.Entries.Add(part);PendingQueue.Edit(state.Pending,part,null);}}
  }else {if(SameValues(desired,before)){RestoreEntry(en,desired);return Task.CompletedTask;}Remember();RestoreEntry(en,desired);PendingQueue.Edit(state.Pending,en,before);}
  RecordInput(en);RefreshReuse();if(before==null){selectedKeys.Clear();selected=null;selectionAnchor=null;editorDirty=false;}Save();Render();status.Text="未保存 "+state.Pending.Count+" 件 · "+settings.SaveSeconds+"秒ごとに保存";return Task.CompletedTask;
 }
 Task DeleteEntry(Entry en) {if(!selectedKeys.Contains(EditingModel.Key(en))){selectedKeys.Clear();selectedKeys.Add(EditingModel.Key(en));selected=en;}return DeleteSelection();}
 async Task<bool> FlushAsync(bool manual) {
  if(communicating)return false;
  if(!ApplyEditor())return false;
  if(state.Pending.Count==0)return true;
  if(service==null||needsRefresh){return manual&&OfferDiscard("接続が必要です。設定または再読込を確認してください。");}
  if(state.Pending.Any(p=>p.Attempted)){return manual&&OfferDiscard("保存結果が不明な変更があります。「再読込」で結果を確認してください。");}
  await BeginProgress("変更をKimaiへ保存しています…");
  try {
   while(state.Pending.Count>0) {
    var p=state.Pending[0];progressText.Text="残り "+state.Pending.Count+" 件を保存中…";
    p.Attempted=true;try {Persist();}catch {p.Attempted=false;throw;}
    try {
     if(p.Delete)await service.DeleteAsync(p.Original);
     else {var saved=await service.WriteAsync(p.Desired,p.Original);saved.LocalKey=p.Desired.LocalKey;int index=state.Entries.FindIndex(e=>e.LocalKey==saved.LocalKey);if(index>=0)state.Entries[index]=saved;}
    }catch(KimaiFailure ex){if(!ex.Uncertain)p.Attempted=false;throw;}
    state.Pending.RemoveAt(0);Persist();
   }
   savePaused=false;status.Text="Kimai保存済み · "+DateTime.Now.ToString("HH:mm:ss");return true;
  }catch(Exception ex){savePaused=true;return OfferDiscard(SafeError(ex)+"\n定期保存を一時停止しました。再読込または今すぐ保存で確認してください。");}
  finally {EndProgress();Render();}
 }
 async Task ChangeWeek(DateTime next) {if(communicating)return;if(!ApplyEditor())return;if(!await FlushAsync(true))return;await ReadRemote(next,false);}
 async Task RefreshRemote() {
  if(communicating)return;
  if(service==null||needsRefresh){await ConnectAsync();if(service==null||needsRefresh)return;}
  if((state.Pending.Any(p=>p.Attempted)||savePaused&&state.Pending.Any(p=>p.Original!=null))&&!await ResolveUncertain())return;
  if(!await FlushAsync(true))return;
  await ReadRemote(week,true,customFrom,customThrough,rangeDayWidth);
 }
 async Task ReadRemote(DateTime target,bool catalog,DateTime? from=null,DateTime? through=null,double? columnWidth=null) {
  if(service==null)return;await BeginProgress("Kimaiから読み込み中…");
  try {
   if(catalog){await LoadCatalog(service,true);await RefreshView();}
   var entries=await service.ReadRangeAsync(from.HasValue?Monday(from.Value):target,through.HasValue?Monday(through.Value).AddDays(7):target.AddDays(7));ClearHistory();week=target;customFrom=from;customThrough=through;rangeDayWidth=columnWidth??rangeDayWidth;if(from.HasValue)dayView=false;statisticsDay=from??target;state.Entries=entries;SeedInputHistory(entries);RefreshReuse();UpdateViewTools();selected=null;needsRefresh=false;savePaused=false;Render();Persist();status.Text="Kimai読込済み · "+entries.Count+" 件";
  }catch(Exception ex){needsRefresh=true;MessageBox.Show(progressWindow,SafeError(ex),"読み込み失敗");}
  finally {EndProgress();}
 }
 async Task<bool> ResolveUncertain() {
  await BeginProgress("保存結果をサーバーに確認しています…");
  try {
   foreach(var p in state.Pending.Where(p=>p.Attempted||savePaused&&p.Original!=null).ToList()) {
    List<Entry> matches;
    if(p.Desired.RemoteId>0) {
     var found=await service.FindEntryAsync(p.Desired.RemoteId);matches=found==null?new List<Entry>():new List<Entry>{found};
    }else matches=(await service.ReadWeekAsync(Monday(p.Desired.Start))).Where(e=>SameValues(e,p.Desired)).ToList();
    Entry actual=matches.Count==1?matches[0]:null;
    bool done=p.Delete?matches.Count==0:actual!=null&&SameValues(actual,p.Desired);
    if(done&&p.Original==null) {
     if(MessageBox.Show(progressWindow,"同じ内容の実績 #"+actual.RemoteId+" が見つかりました。\nこの実績を今回の作成結果として採用しますか？\n別の実績の場合は「いいえ」で保留します。","作成結果の照合",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return false;
    }
    if(done) {
     if(actual!=null&&!p.Delete){actual.LocalKey=p.Desired.LocalKey;int index=state.Entries.FindIndex(e=>e.LocalKey==actual.LocalKey);if(index>=0)state.Entries[index]=actual;}
     state.Pending.Remove(p);
    } else {
     var answer=MessageBox.Show(progressWindow,"サーバー上で変更の完了を確認できませんでした。\n"+p.Desired.Project+" / "+p.Desired.Start.ToString("g")+"\n再送信を許可しますか？ 作成の場合はKimai画面で重複がないことを確認してください。\n「いいえ」は変更を保持して終了を中止します。","保存結果の確認",MessageBoxButton.YesNo);
     if(answer!=MessageBoxResult.Yes)return false;
     if(actual!=null)p.Original=actual;
     p.Attempted=false;
    }
    Persist();
   }
   savePaused=false;return true;
  }catch(Exception ex){MessageBox.Show(progressWindow,SafeError(ex),"保存結果の確認失敗");return false;}
  finally {EndProgress();Render();}
 }
 internal static bool SameValues(Entry a,Entry b)=>a.ProjectId==b.ProjectId&&a.ActivityId==b.ActivityId&&a.Start==b.Start&&a.Minutes==b.Minutes&&(a.Note??"")==(b.Note??"");
 void StoreSettings() {PortableToken.Migrate(settings,DataDirectory);Directory.CreateDirectory(DataDirectory);File.WriteAllText(SettingsFile+".tmp",JsonSerializer.Serialize(settings));File.Move(SettingsFile+".tmp",SettingsFile,true);}
}






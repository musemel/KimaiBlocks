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
 public string Url {get;set;}="";
 public string Username {get;set;}="";
 public bool Legacy {get;set;}
 public bool AllowHttp {get;set;}
 public string ProtectedToken {get;set;}="";
 public int SaveSeconds {get;set;}=60;
 public int CatalogMinutes {get;set;}=60;
 public CalendarRules Calendar {get;set;}=new CalendarRules();
 public bool ShowWeekends {get;set;}=true;
 public bool ShowStatistics {get;set;}=true;
 public int UserId {get;set;}
}
public partial class Blocks {
 KimaiService service;
 bool communicating,needsRefresh,closingApproved,closingRequested,savePaused,demoMode;
 ConnectionSettings settings=new ConnectionSettings();
 DispatcherTimer saveTimer=new DispatcherTimer();
 TextBlock connectionBadge=new TextBlock();
 Action<string> testError;
 void ShowSaveError(string text) {if(testError!=null)testError(text);else MessageBox.Show(progressWindow??this,text,"保存失敗");}
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
  var create=new MenuItem {Header="プロジェクト追加…",Foreground=Brushes.Black};create.Click+=async(s,e)=>await ProjectDialog();item.Items.Add(create);
  var calendar=new MenuItem {Header="休日・休み時間…",Foreground=Brushes.Black};calendar.Click+=(s,e)=>CalendarDialog();item.Items.Add(calendar);menu.Items.Add(item);buttons.Children.Add(menu);
  var config=new MenuItem {Header="設定…",Foreground=Brushes.Black};config.Click+=(s,e)=>ConnectionDialog();item.Items.Add(config);
  var stats=new MenuItem {Header="右の統計パネルを表示／非表示",Foreground=Brushes.Black};stats.Click+=(s,e)=>SetStatisticsVisible(!settings.ShowStatistics);item.Items.Add(stats);
  buttons.Children.Add(ButtonOf("再読込",async()=>await RefreshRemote()));
  buttons.Children.Add(ButtonOf("今すぐ保存",async()=>await FlushAsync(true)));
  DockPanel.SetDock(buttons,Dock.Right);top.Children.Insert(0,buttons);
  connectionBadge.Text="Kimai未接続";connectionBadge.Foreground=BrushOf("#C7D7E6");connectionBadge.VerticalAlignment=VerticalAlignment.Center;top.Children.Add(connectionBadge);
  saveTimer.Tick+=async(s,e)=>{if(!communicating&&!savePaused&&!closingRequested&&OwnedWindows.Count==0&&state.Pending.Count>0)await FlushAsync(false);};
  Loaded+=async(s,e)=>{if(!demo)await StartupAsync();};
  Closing+=async(s,e)=>{
   if(demoMode||closingApproved)return;e.Cancel=true;
   if(communicating){MessageBox.Show(this,"読み込み・保存が完了するまで終了できません。","終了待機");return;}
   if(closingRequested)return;closingRequested=true;
   try {if(!await FlushAsync(true))return;Persist();closingApproved=true;_ = Dispatcher.BeginInvoke(new Action(Close));}
   catch(Exception ex){MessageBox.Show(this,SafeError(ex)+"\n保存できないため終了を中止しました。","終了できません");}
   finally {closingRequested=false;}
  };
  Closed+=(s,e)=>{saveTimer.Stop();service?.Dispose();};
 }
 async Task StartupAsync() {
  try {
   if(!File.Exists(SettingsFile)){ConnectionDialog();return;}
   settings=JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(SettingsFile))??new ConnectionSettings();
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
  state=loaded;file=account;Projects=state.CachedProjects;
  // Pending desired values take precedence over the last downloaded snapshot.
  foreach(var change in state.Pending.Where(p=>!p.Delete)) {
   var existing=state.Entries.FindIndex(e=>e.LocalKey!=null&&e.LocalKey==change.Desired.LocalKey);
   if(existing>=0)state.Entries[existing]=change.Desired;else state.Entries.Add(change.Desired);
  }
  if(state.CachedWeek!=default)week=state.CachedWeek;
  Render();status.Text="キャッシュ表示（接続確認中）";
 }
 async Task ConnectAsync() {
  if(communicating)return;
  await BeginProgress("Kimaiに接続しています…");KimaiService candidate=null;
  try {
   string token=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(settings.ProtectedToken),null,DataProtectionScope.CurrentUser));
   candidate=new KimaiService(settings.Url,token,settings.Username,settings.Legacy,allowHttp:settings.AllowHttp);
   await candidate.InitializeAsync(false);
   if(state.Pending.Count>0&&file!=AccountFile(candidate.BaseUrl,candidate.Me.Id.Value))throw new KimaiFailure("未保存の変更があるため接続先を変更できません。");
   LoadAccount(candidate.BaseUrl,candidate.Me.Id.Value);
   progressText.Text="プロジェクトとアクティビティを読み込み中…";
   await LoadCatalog(candidate,false);
   service?.Dispose();service=candidate;candidate=null;
   settings.UserId=service.Me.Id.Value;StoreSettings();ConfigureTimer();
   connectionBadge.Text="接続: "+service.Me.Username+" · "+service.Me.Timezone;
   needsRefresh=false;savePaused=state.Pending.Any(p=>p.Attempted);
   await RefreshView();
   if(state.Pending.Count==0){progressText.Text="今週の実績を読み込み中…";week=Monday(DateTime.Today);state.Entries=await service.ReadWeekAsync(week);}
   Render();Persist();status.Text=state.Pending.Count>0?"未保存の変更をキャッシュから復元しました":"Kimai読込済み";
   if(savePaused)MessageBox.Show(progressWindow,"送信途中の変更を復元しました。「再読込」で保存結果を確認してください。","保存結果の確認が必要です");
  }catch(Exception ex){needsRefresh=true;savePaused=true;MessageBox.Show(progressWindow,SafeError(ex),"接続・読み込み失敗");}
  finally {candidate?.Dispose();EndProgress();}
 }
 async Task RefreshView() {
  var old=Projects;Projects=service.Projects.Where(p=>p.Visible!=false).Select(p=>service.ProjectName(p.Id.Value)).ToArray();
  foreach(var p in Projects.Except(old))if(!state.Collapsed.Contains("project:"+p))state.Collapsed.Add("project:"+p);
  await Dispatcher.Yield(DispatcherPriority.Background);PopulateProjectList();Populate();Render();
 }
 bool CanEdit(Entry en=null) {
  if(demoMode)return true;
  if(communicating||service==null||needsRefresh){status.Text="接続・再読込を完了してから編集してください。";return false;}
  if(state.Pending.Any(p=>p.Attempted)){MessageBox.Show(this,"保存結果が未確認です。「再読込」で確認してください。");return false;}
  if(en?.ReadOnlyReason!=null){MessageBox.Show(this,"読み取り専用: "+en.ReadOnlyReason);return false;}return true;
 }
 Task CommitEntry(Entry en,Entry before) {
  if(!AcceptSchedule(en,before)) {
   if(before==null)state.Entries.Remove(en);else RestoreEntry(en,before);
   Render();return Task.CompletedTask;
  }
  if(demoMode){Render();return Task.CompletedTask;}
  PendingQueue.Edit(state.Pending,en,before);
  Save();Render();status.Text="未保存 "+state.Pending.Count+" 件 · "+settings.SaveSeconds+"秒ごとに保存";return Task.CompletedTask;
 }
 Task DeleteEntry(Entry en) {
  if(!CanEdit(en))return Task.CompletedTask;
  if(MessageBox.Show(this,"この実績を削除しますか？（次回保存時に反映）","実績の削除",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return Task.CompletedTask;
  PendingQueue.Delete(state.Pending,en);
  state.Entries.Remove(en);selected=null;Save();Render();status.Text="未保存 "+state.Pending.Count+" 件";return Task.CompletedTask;
 }
 async Task<bool> FlushAsync(bool manual) {
  if(communicating)return false;
  if(state.Pending.Count==0)return true;
  if(service==null||needsRefresh){if(manual)MessageBox.Show(this,"接続が必要です。設定または再読込を確認してください。変更は保持されています。","保存できません");return false;}
  if(state.Pending.Any(p=>p.Attempted)){if(manual)MessageBox.Show(this,"保存結果が不明な変更があります。「再読込」で結果を確認してください。終了を中止しました。","保存できません");return false;}
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
  }catch(Exception ex){savePaused=true;ShowSaveError(SafeError(ex)+"\n変更を保持し、定期保存を一時停止しました。再読込または今すぐ保存で確認してください。\n保存が完了するまで終了できません。");return false;}
  finally {EndProgress();Render();}
 }
 async Task ChangeWeek(DateTime next) {if(communicating)return;if(!await FlushAsync(true))return;await ReadRemote(next,false);}
 async Task RefreshRemote() {
  if(communicating)return;
  if(service==null||needsRefresh){await ConnectAsync();if(service==null||needsRefresh)return;}
  if((state.Pending.Any(p=>p.Attempted)||savePaused&&state.Pending.Any(p=>p.Original!=null))&&!await ResolveUncertain())return;
  if(!await FlushAsync(true))return;
  await ReadRemote(week,true);
 }
 async Task ReadRemote(DateTime target,bool catalog) {
  if(service==null)return;await BeginProgress("Kimaiから読み込み中…");
  try {
   if(catalog){await LoadCatalog(service,true);await RefreshView();}
   var entries=await service.ReadWeekAsync(target);week=target;state.Entries=entries;selected=null;needsRefresh=false;savePaused=false;Render();Persist();status.Text="Kimai読込済み · "+entries.Count+" 件";
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
 internal static bool SameValues(Entry a,Entry b)=>a.ProjectId==b.ProjectId&&a.ActivityId==b.ActivityId&&a.Start==b.Start&&a.Minutes==b.Minutes&&(a.Note??"")==(b.Note??"")&&a.Billable==b.Billable;
 void StoreSettings() {Directory.CreateDirectory(DataDirectory);File.WriteAllText(SettingsFile+".tmp",JsonSerializer.Serialize(settings));File.Move(SettingsFile+".tmp",SettingsFile,true);}
 void ConnectionDialog() {
  if(communicating)return;
  var w=new Window {Title="設定",Owner=this,Width=550,Height=750,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel {Margin=new Thickness(22)};w.Content=panel;
  var url=new TextBox {Text=settings.Url};var username=new TextBox {Text=settings.Username};var token=new PasswordBox();
  try {token.Password=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(settings.ProtectedToken),null,DataProtectionScope.CurrentUser));}catch{}
  var legacy=new CheckBox {Content="旧認証方式（ユーザー名＋APIトークン）",IsChecked=settings.Legacy};
  var http=new CheckBox {Content="この接続先でHTTPを許可",IsChecked=settings.AllowHttp};url.TextChanged+=(s,e)=>http.IsChecked=false;
  var seconds=new TextBox {Text=settings.SaveSeconds.ToString()};var minutes=new TextBox {Text=settings.CatalogMinutes.ToString()};
  string[] labels={"Kimai URL","ユーザー名（旧方式のみ）","APIトークン（Windowsユーザー用に暗号化して保存）","保存間隔（秒、10〜3600）","一覧キャッシュの有効期間（分、1〜1440）"};Control[] fields={url,username,token,seconds,minutes};
  for(int i=0;i<fields.Length;i++){panel.Children.Add(Label(labels[i],12));fields[i].Padding=new Thickness(6);panel.Children.Add(fields[i]);}
  var weekends=new CheckBox {Content="カレンダーに土日を表示",IsChecked=settings.ShowWeekends,Margin=new Thickness(4,10,4,10)};panel.Children.Add(weekends);
  panel.Children.Add(ButtonOf("休日・休み時間の設定…",CalendarDialog));
  panel.Children.Add(legacy);panel.Children.Add(http);panel.Children.Add(Label("起動時に自動接続します。変更は定期保存し、終了時にも保存します。\n「再読込」は一覧キャッシュも更新します。",12));
  panel.Children.Add(ButtonOf("保存して接続",async()=>{
   if(!int.TryParse(seconds.Text,out int sec)||sec<10||sec>3600||!int.TryParse(minutes.Text,out int min)||min<1||min>1440){MessageBox.Show(w,"保存間隔とキャッシュ期間を範囲内で指定してください。");return;}
   try {
    string normalized=KimaiService.NormalizeUrl(url.Text,http.IsChecked==true);
    if(state.Pending.Count>0&&(normalized!=settings.Url||username.Text!=settings.Username||token.Password!=DecodeToken())){MessageBox.Show(w,"接続情報の変更前に未保存の実績を保存してください。");return;}
    settings=new ConnectionSettings {ShowWeekends=weekends.IsChecked==true,ShowStatistics=settings.ShowStatistics,Calendar=settings.Calendar,Url=normalized,Username=username.Text,Legacy=legacy.IsChecked==true,AllowHttp=http.IsChecked==true,SaveSeconds=sec,CatalogMinutes=min,UserId=normalized==settings.Url?settings.UserId:0,ProtectedToken=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Password),null,DataProtectionScope.CurrentUser))};
    StoreSettings();w.Close();await ConnectAsync();
   }catch(Exception ex){MessageBox.Show(w,SafeError(ex),"設定保存失敗");}
  }));w.ShowDialog();
 }
 string DecodeToken(){try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(settings.ProtectedToken),null,DataProtectionScope.CurrentUser));}catch{return "";}}
}


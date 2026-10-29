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

public partial class Blocks {
 static string AppVersion=>Assembly.GetExecutingAssembly().GetName().Version.ToString();
 readonly DispatcherTimer clockTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
 Border nowLine;TextBlock rightClock;
 void StartClock(){clockTimer.Tick+=(s,e)=>DrawNow();clockTimer.Start();}
 void DrawNow() {
  if(nowLine!=null)board.Children.Remove(nowLine);
  DateTime now=DateTime.Now;
  try {if(!string.IsNullOrEmpty(service?.Me?.Timezone))now=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(service.Me.Timezone));}catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
  if(rightClock!=null)rightClock.Text=now.ToString("M/d (ddd)  HH:mm:ss");
  int day=(now.Date-DisplayStart).Days;if(day<0||day>=DisplayDayCount)return;
  double y=now.TimeOfDay.TotalHours*Hour;
  nowLine=new Border {Height=2,Width=DayWidth,Background=Brushes.Crimson,IsHitTestVisible=false};Canvas.SetLeft(nowLine,Gutter+day*DayWidth);Canvas.SetTop(nowLine,y);Panel.SetZIndex(nowLine,1000);board.Children.Add(nowLine);
 }
 void LoadDefaults(string directory=null) {try{string path=Path.Combine(directory??AppContext.BaseDirectory,"defaults.json");defaultsJson=File.Exists(path)?File.ReadAllText(path):null;settings=SettingsLayers.Load(defaultsJson);}catch(Exception ex){throw new ArgumentException("defaults.json を読み込めません: "+ex.Message);}}
 bool checkingUpdates;
 async Task CheckUpdates(bool manual) {
  if(checkingUpdates)return;
  if(string.IsNullOrWhiteSpace(EffectiveUpdateFolder)){if(manual)MessageBox.Show(this,"設定でアップデート確認フォルダを指定してください。","アップデート");return;}
  checkingUpdates=true;string folder=EffectiveUpdateFolder;
  try {
   var read=Task.Run(()=>{try {return (Version:UpdateVersion.Read(folder),Error:(string)null);}catch(Exception){return (Version:(Version)null,Error:"確認先のKimaiBlocks.dll または KimaiBlocks.exe のバージョンを読み込めません。パスとアクセス権を確認してください。");}});
   if(await Task.WhenAny(read,Task.Delay(TimeSpan.FromSeconds(8)))!=read){if(manual)MessageBox.Show(this,"確認がタイムアウトしました。","アップデート");return;}
   var result=await read;
   if(!IsVisible)return;
   if(result.Error!=null){if(manual)MessageBox.Show(this,result.Error,"アップデート");return;}
   if(result.Version>Version.Parse(AppVersion)||manual)ShowUpdateDialog(folder,result.Version);
  }finally {checkingUpdates=false;}
 }
 void ShowUpdateDialog(string folder,Version available) {
  var w=new Window {Title="アップデート",Owner=this,Width=760,Height=Math.Min(720,SystemParameters.WorkArea.Height-40),MinWidth=540,MinHeight=420,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var root=new DockPanel {Margin=new Thickness(20)};w.Content=root;
  var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  header.Children.Add(Label(available>Version.Parse(AppVersion)?"新しいバージョンがあります":"新しいバージョンはありません",17));
  header.Children.Add(Label("現在: "+AppVersion+"    配布版: "+available,12));
  var help=Label("保存して終了後、配布フォルダの一式をコピーしてください。\nsettings.override.json がある場合は一緒にコピーしてください。",12);help.TextWrapping=TextWrapping.Wrap;header.Children.Add(help);
  var footer=new StackPanel {Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
  void PathRow(string label,string path){var row=new DockPanel {Margin=new Thickness(4,2,4,2)};var caption=Label(label,11);caption.Width=125;DockPanel.SetDock(caption,Dock.Left);row.Children.Add(caption);row.Children.Add(new TextBox {Text=path,IsReadOnly=true,ToolTip=path,Padding=new Thickness(4),VerticalContentAlignment=VerticalAlignment.Center});footer.Children.Add(row);}
  PathRow("配布フォルダ",folder);PathRow("実行中のEXEフォルダ",AppContext.BaseDirectory);
  var buttons=new WrapPanel {Margin=new Thickness(0,8,0,0)};footer.Children.Add(buttons);
  buttons.Children.Add(ButtonOf("配布フォルダを開く",()=>{try{if(!Directory.Exists(folder))throw new IOException("配布フォルダにアクセスできません。");Process.Start(new ProcessStartInfo {FileName=Path.GetFullPath(folder),UseShellExecute=true});}catch(Exception ex){MessageBox.Show(w,SafeError(ex),"フォルダを開けません");}}));
  buttons.Children.Add(ButtonOf("実行中のEXEフォルダを開く",()=>{try{Process.Start(new ProcessStartInfo {FileName=AppContext.BaseDirectory,UseShellExecute=true});}catch(Exception ex){MessageBox.Show(w,SafeError(ex),"フォルダを開けません");}}));
  var close=ButtonOf("閉じる",()=>w.Close());close.IsCancel=true;buttons.Children.Add(close);
  var body=new DockPanel {Margin=new Thickness(0,10,0,0)};root.Children.Add(body);var title=Label("更新情報（updates.txt）",13);DockPanel.SetDock(title,Dock.Top);body.Children.Add(title);
  var notes=new TextBox {Name="UpdateNotes",Text="更新情報を読み込んでいます…",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(4),Padding=new Thickness(12)};body.Children.Add(notes);
  w.Loaded+=async(s,e)=>{
   var reading=Task.Run(()=>ReadUpdateNotes(folder));
   string text=await Task.WhenAny(reading,Task.Delay(TimeSpan.FromSeconds(8)))==reading?await reading:"更新情報の読み込みがタイムアウトしました。";
   if(!w.IsVisible)return;notes.Text=text==null?"配布フォルダにupdates.txtがありません。":string.IsNullOrWhiteSpace(text)?"更新情報ファイルは空です。":text;
  };
  ShowDialogLayout(w);
 }
 internal static string ReadUpdateNotes(string folder) {
  try {
   using var reader=new StreamReader(Path.Combine(folder,"updates.txt"),System.Text.Encoding.UTF8,true);
   const int limit=65536;var buffer=new char[limit+1];int count=0,read;
   while(count<buffer.Length&&(read=reader.Read(buffer,count,buffer.Length-count))>0)count+=read;
   return new string(buffer,0,Math.Min(count,limit))+(count>limit?"\n\n（表示上限を超えたため省略しました。全文は配布フォルダのupdates.txtをご覧ください。）":"");
  }catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}
  catch(Exception){return "更新情報を読み込めませんでした。配布フォルダのupdates.txtとアクセス権を確認してください。";}
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
  bool discard=false;panel.Children.Add(ButtonOf("変更を保持して戻る",()=>w.Close()));panel.Children.Add(ButtonOf("未保存変更を破棄…",()=>{if(MessageBox.Show(w,"残っている未保存変更をすべて破棄しますか？この操作は取り消せません。","変更の破棄",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes){discard=true;w.Close();}}));ShowDialogLayout(w);
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
   var entries=await candidate.ReadRangeAsync(LoadedStart,LoadedUntil);
   state.Entries=entries;state.Pending=new System.Collections.Generic.List<PendingChange>();file=target;
   var previous=service;service=candidate;candidate=null;
   try {await RefreshView();Persist();committed=true;}catch {candidate=service;service=previous;throw;}
   ClearHistory();previous?.Dispose();File.Delete(file+".bak");File.Delete(file+".catalog");needsRefresh=false;savePaused=false;ConfigureTimer();selected=null;status.Text="サーバーから再取得しました";Render();
  }catch(Exception ex){if(!committed){file=oldFile;state.Entries=oldEntries;state.Pending=oldPending;Projects=oldProjects;state.Hidden=oldHidden;state.Collapsed=oldCollapsed;}needsRefresh=true;MessageBox.Show(progressWindow,SafeError(ex),"再取得失敗");}
  finally {candidate?.Dispose();EndProgress();Populate();PopulateProjectList();Render();}
 }
}







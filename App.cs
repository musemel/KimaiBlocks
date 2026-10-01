using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

[DataContract] public class Entry {
 [DataMember] public string LocalKey;
 [DataMember] public string Project;
 [DataMember] public string Activity;
 [DataMember] public DateTime Start;
 [DataMember] public int Minutes;
 [DataMember] public string Note = "";
 [DataMember] public int RemoteId;
 [DataMember] public int ProjectId;
 [DataMember] public int ActivityId;
 [DataMember] public bool Billable;
 [DataMember] public bool BillableOverride;
 [DataMember] public string Fingerprint;
 [DataMember] public string ReadOnlyReason;
 public Entry Copy() { return (Entry)MemberwiseClone(); }
}
[DataContract] public class State {
 [DataMember] public List<Entry> Entries = new List<Entry>();
 [DataMember] public List<string> Hidden = new List<string>();
 [DataMember] public List<string> Favorites = new List<string>();
 [DataMember] public List<string> Folders = new List<string>();
 [DataMember] public Dictionary<string,string> ProjectFolders = new Dictionary<string,string>();
 [DataMember] public List<string> Collapsed = new List<string>();
 [DataMember] public Dictionary<string,string> FixedColors = new Dictionary<string,string>();
 [DataMember] public List<PendingChange> Pending = new List<PendingChange>();
 [DataMember(EmitDefaultValue=false)] public DateTime CachedWeek;
 [DataMember] public string[] CachedProjects = Array.Empty<string>();
 [OnDeserialized] void Upgrade(StreamingContext context) {
  FixedColors ??= new Dictionary<string,string>();
  Pending = Pending ?? new List<PendingChange>();
  CachedProjects = CachedProjects ?? Array.Empty<string>();
  Folders = Folders ?? new List<string>();
  ProjectFolders = ProjectFolders ?? new Dictionary<string,string>();
  Collapsed = Collapsed ?? new List<string>();
 }
 public void RemoveFolder(string name) {
  Folders.Remove(name);
  foreach(var project in ProjectFolders.Where(x=>x.Value==name).Select(x=>x.Key).ToList()) ProjectFolders.Remove(project);
  Collapsed.Remove("folder:"+name);
 }
}
public partial class Blocks : Window {
 static string[] Projects = { "Webサイト制作", "製品開発", "社内業務", "調査・学習" };
 static string[] Activities = { "設計", "実装", "レビュー", "ミーティング" };
 static string[] Colors = { "#B9DCF9", "#C4E8B5", "#FFD9AC", "#DBCDF8" };
 State state; DateTime week; Canvas board = new Canvas {Background=Brushes.Transparent}; Grid headers = new Grid();
 TextBox search = new TextBox(); CheckBox favorites = new CheckBox();
 TextBlock period = new TextBlock(), total = new TextBlock(), status = new TextBlock();
 ScrollViewer calendarScroll; Entry selected; bool busy; const int SlotMinutes = 5; static double Hour = 144; const double Gutter = 58;
 string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KimaiBlocks", "state.json");
 Brush Ink = new SolidColorBrush(Color.FromRgb(32, 49, 68));
 [STAThread] public static void Main(string[] args) {
  if(args.Contains("--sync-test")) {RunSyncTests();return;}
  if (args.Contains("--self-test")) { UpdateVersion.Tests(); EditingModel.Tests(); ReportTests.Run().GetAwaiter().GetResult(); CommentStatistics.Tests(); PortableToken.Tests(); AccountProfile.Tests(); BlockOperations.Tests(); CalendarRules.Tests(); PendingQueue.Tests(); SidebarTests(); KimaiServiceTests.Run().GetAwaiter().GetResult(); if (HitMode(1,12)!=1 || HitMode(6,12)!=0 || HitMode(11,12)!=2 || Snap(63)!=65 || Snap(62)!=60 || Snap(5)!=5 || RoundDelta(-6)!=-5 || ResizeDuration(1435,5,10)!=5 || ResizeDuration(540,10,-20)!=5 || !ValidTime(TimeSpan.FromHours(9)+TimeSpan.FromMinutes(5),5) || ValidTime(TimeSpan.FromHours(9)+TimeSpan.FromMinutes(1),5) || ValidTime(TimeSpan.FromHours(23)+TimeSpan.FromMinutes(55),10) || Snap(-1)!=0 || Monday(new DateTime(2026,9,20))!=new DateTime(2026,9,14)) Environment.Exit(1); return; }
  if(args.Contains("--render")) { var app=new Application();var window=new Blocks(true);window.state.Folders.Add("開発案件");window.state.ProjectFolders[Projects[0]]="開発案件";window.state.ProjectFolders[Projects[1]]="開発案件";window.state.Collapsed.Add("project:"+Projects[1]);window.Populate();window.state.Entries.Add(new Entry { Project=Projects[0], Activity="レビュー", Start=window.week.AddHours(10).AddMinutes(35), Minutes=5 });var view=(FrameworkElement)window.Content;view.Width=1320;view.Height=900;view.Measure(new Size(1320,900));view.Arrange(new Rect(0,0,1320,900));window.Render();view.UpdateLayout();window.calendarScroll.ScrollToVerticalOffset(8*Hour);view.UpdateLayout();var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap(1320,900,96,96,PixelFormats.Pbgra32);bmp.Render(view);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));using(var f=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png")))png.Save(f);return; }
  new Application().Run(new Blocks());
 }
 static DateTime Monday(DateTime d) { return d.Date.AddDays(-((int)d.DayOfWeek+6)%7); }
 static int RoundDelta(double m) { return (int)Math.Round(m/SlotMinutes, MidpointRounding.AwayFromZero)*SlotMinutes; }
 static int Snap(double m) { return Math.Max(0,RoundDelta(m)); }
 static int ResizeDuration(int start, int duration, int delta) { return Math.Max(SlotMinutes,Math.Min(1440-start,duration+delta)); }
 static bool ValidTime(TimeSpan time, int duration) { return time.TotalMinutes>=0 && time.TotalMinutes<1440 && time.Ticks%TimeSpan.FromMinutes(SlotMinutes).Ticks==0 && duration>=SlotMinutes && duration%SlotMinutes==0 && time.TotalMinutes+duration<=1440; }
 static int HitMode(double y, double height) { double grip=Math.Min(6,height/4); return y<grip?1:y>height-grip?2:0; }
 Brush BrushOf(string c) { return (Brush)new BrushConverter().ConvertFromString(c); }
 Button ButtonOf(string text, Action action) { var b=new Button { Content=text, Padding=new Thickness(12,7,12,7), Margin=new Thickness(3), Background=Brushes.White, BorderBrush=BrushOf("#DCE3EA"), Cursor=Cursors.Hand }; b.Click+=(s,e)=>action(); return b; }
 TextBlock Label(string t, double size) { return new TextBlock { Text=t, FontSize=size, Foreground=Ink, Margin=new Thickness(4), VerticalAlignment=VerticalAlignment.Center }; }
 public Blocks(bool demo = false) {
  Title="Kimai Blocks — "+AppVersion; Width=1540; Height=900; MinWidth=1240; MinHeight=650; Background=BrushOf("#F5F7FA"); FontFamily=new FontFamily("Yu Gothic UI"); FontSize=13;
  Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/KimaiBlocks.ico"));
  week=Monday(DateTime.Today); if(demo)Load(true);else {state=new State();Projects=Array.Empty<string>();file=null;}
  var root=new DockPanel { Background=Background }; Content=root;
  var top=new DockPanel { Background=BrushOf("#142B40"), LastChildFill=true, Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
  var title=Label("▦  Kimai Blocks",22); title.Foreground=Brushes.White; title.Margin=new Thickness(20,16,20,16); top.Children.Add(title);
  AddConnectionTools(top,demo);
  BuildEditToolbar(root);
  status.Margin=new Thickness(16,8,16,8); status.Text="一覧からドラッグして配置 • 上下端で時間変更 • ダブルクリックで編集 • Deleteで削除"; DockPanel.SetDock(status,Dock.Bottom); root.Children.Add(status);
  BuildSidebar(root);
  BuildStatistics(root);
  var main=new DockPanel { Margin=new Thickness(0,0,14,0) }; root.Children.Add(main);
  var toolbar=new DockPanel { Margin=new Thickness(0,0,0,10) }; DockPanel.SetDock(toolbar,Dock.Top); main.Children.Add(toolbar);
  var nav=new StackPanel { Orientation=Orientation.Horizontal }; nav.Children.Add(ButtonOf("‹",async()=>await NavigateCalendar(-1))); nav.Children.Add(ButtonOf("今日",async()=>await NavigateToday())); nav.Children.Add(ButtonOf("›",async()=>await NavigateCalendar(1))); period.FontSize=19; period.Margin=new Thickness(14,0,14,0); period.VerticalAlignment=VerticalAlignment.Center; var weekButton=ButtonOf("",SelectWeek);weekButton.Content=period;weekButton.Padding=new Thickness(0,4,0,4);weekButton.ToolTip="カレンダーで表示週を選択";nav.Children.Add(weekButton); toolbar.Children.Add(nav);

  total.HorizontalAlignment=HorizontalAlignment.Right; total.VerticalAlignment=VerticalAlignment.Center; toolbar.Children.Add(total);
  headers.Height=52; headers.Background=Brushes.White; DockPanel.SetDock(headers,Dock.Top); main.Children.Add(headers);
  var scroll=new ScrollViewer { Content=board, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, Background=Brushes.White }; main.Children.Add(scroll); calendarScroll=scroll;
  board.Height=24*Hour; board.AllowDrop=true; board.SizeChanged+=(s,e)=>{if(!busy)Render();}; board.Drop+=DropWork;
  board.DragOver+=(s,e)=>{e.Effects=IsWorkDrop(e.Data)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
  board.Focusable=true;
  SetupRangeSelection();
  SetupKeys();
  Loaded+=(s,e)=>{Render();scroll.ScrollToVerticalOffset(8*Hour);}; Populate();
 }
 void Load(bool demo) {
  try { if(!demo && File.Exists(file)) { using(var f=File.OpenRead(file)) state=(State)new DataContractJsonSerializer(typeof(State)).ReadObject(f); if(state==null || state.Entries==null || state.Hidden==null || state.Favorites==null) throw new Exception(); return; } }
  catch { MessageBox.Show("保存データを読み込めませんでした。元ファイルを保護するため終了します。"); Environment.Exit(1); }
  state=new State(); state.Favorites.Add(Projects[0]+"|設計");
  for(int d=0;d<5;d++) { state.Entries.Add(new Entry { Project=Projects[d%4],Activity="設計",Start=week.AddDays(d).AddHours(9),Minutes=90 });state.Entries.Add(new Entry {Project=Projects[(d+1)%4],Activity="実装",Start=week.AddDays(d).AddHours(11),Minutes=120});state.Entries.Add(new Entry {Project=Projects[d%4],Activity="レビュー",Start=week.AddDays(d).AddHours(14),Minutes=60}); }
 }
 void Save() { try { Persist(); } catch(Exception ex) { status.Text="キャッシュ保存失敗: "+SafeError(ex);if(state.Pending.Count>0)OfferDiscard(status.Text);else MessageBox.Show(this,status.Text,"保存失敗"); } }
 void Persist() {
  if(file==null)return;
  state.CachedWeek=week;state.CachedProjects=Projects;
  Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";
  using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(State)).WriteObject(f,state);
  if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);
 }
 bool dayView;DateTime displayDay;
 DateTime DisplayStart=>dayView?(displayDay>=week&&displayDay<week.AddDays(7)?displayDay:week):week;
 int DisplayDayCount=>dayView?1:settings.ShowWeekends?7:5;
 double DayWidth {get{return Math.Max(1,(board.ActualWidth-Gutter)/DisplayDayCount);}}
 void Render() {
  if(busy||inlineComment!=null)return; busy=true;
  try {
   RefreshColors();entryBoxes.Clear();
   board.Children.Clear(); headers.Children.Clear(); headers.ColumnDefinitions.Clear();headers.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(Gutter)});
   period.Text=dayView?DisplayStart.ToString("yyyy/M/d (ddd)"):week.ToString("yyyy/M/d")+" – "+week.AddDays(6).ToString("M/d");
   var entries=VisibleEntries();RenderStatistics(entries);total.Text="週合計  "+(entries.Sum(x=>x.Minutes)/60.0).ToString("0.##")+" h  ·  5分刻み";
   DrawUnavailable();
   for(int d=0;d<DisplayDayCount;d++) {headers.ColumnDefinitions.Add(new ColumnDefinition());var h=Label(DisplayStart.AddDays(d).ToString("M/d (ddd)"),14);h.HorizontalAlignment=HorizontalAlignment.Center;if(DisplayStart.AddDays(d)==DateTime.Today)h.Foreground=BrushOf("#1971C2");DateTime headerDay=DisplayStart.AddDays(d);h.Cursor=Cursors.Hand;h.ToolTip="この日の統計を表示";h.MouseLeftButtonDown+=(s,e)=>{statisticsDay=headerDay;SetStatisticsVisible(true);statisticsTabs.SelectedIndex=1;RenderStatistics(VisibleEntries());};Grid.SetColumn(h,d+1);headers.Children.Add(h);var line=new Border{Width=1,Height=board.Height,Background=BrushOf("#E8EDF2")};Canvas.SetLeft(line,Gutter+d*DayWidth);board.Children.Add(line);}
   for(int h=0;h<24;h++) {var t=Label(h.ToString("00")+":00",11);Canvas.SetTop(t,h*Hour);board.Children.Add(t);var line=new Border {Height=1,Width=DisplayDayCount*DayWidth,Background=BrushOf("#E8EDF2")};Canvas.SetLeft(line,Gutter);Canvas.SetTop(line,h*Hour);board.Children.Add(line);}
   for(int minute=SlotMinutes;minute<1440;minute+=SlotMinutes) { if(minute%60==0)continue;var line=new Border { Height=1, Width=DisplayDayCount*DayWidth, Background=BrushOf(minute%30==0?"#DCE4ED":"#F0F3F7"), IsHitTestVisible=false }; Canvas.SetLeft(line,Gutter);Canvas.SetTop(line,minute/60.0*Hour);board.Children.Add(line); }
   DrawSelectedRange();
   foreach(var en in entries.Where(e=>e.Start.Date>=DisplayStart&&(e.Start.Date-DisplayStart).Days<DisplayDayCount)) DrawEntry(en,entries);
   DrawNow();RefreshSelection();
  } finally {busy=false;}
 }
 async void DropWork(object sender,DragEventArgs e) {
  if(!CanEdit()||!ApplyEditor())return;var pos=e.GetPosition(board);if(pos.X<Gutter)return;
  var work=ResolveDroppedWork(e.Data);if(work==null)return;e.Handled=true;
  int d=Math.Clamp((int)((pos.X-Gutter)/DayWidth),0,DisplayDayCount-1);int minute=Math.Clamp(Snap(pos.Y/Hour*60),0,1435);
  var en=new Entry {Project=work[0],Activity=work[1],Start=rangeStart??DisplayStart.AddDays(d).AddMinutes(minute),Minutes=rangeStart.HasValue?rangeMinutes:Math.Min(60,1440-minute)};
  if(!AssignIds(en))return;await FinishBlockDrag(null,null,en,true,0);rangeStart=null;Render();
 }
 async void Delete(Entry en) {await DeleteEntry(en);}
 void Edit(Entry en) {
  if(!CanEdit(en))return;Entry before=en.Copy();
  var w=new Window {Title="実績を編集",Width=380,Height=570,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var panel=new StackPanel {Margin=new Thickness(22)};w.Content=panel;
  var project=new ComboBox {ItemsSource=Projects,SelectedItem=en.Project};var activity=new ComboBox {ItemsSource=ActivitiesFor(en.Project),SelectedItem=en.Activity};var date=new DatePicker {SelectedDate=en.Start.Date};var start=new TextBox {Text=en.Start.ToString("HH:mm")};var minutes=new TextBox {Text=en.Minutes.ToString()};var note=new TextBox {Text=en.Note,Height=65,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  project.SelectionChanged+=(s,e)=>{activity.ItemsSource=ActivitiesFor((string)project.SelectedItem);activity.SelectedIndex=0;};
  string[] labels={"プロジェクト","アクティビティ","日付（未来も入力できます）","開始時刻 HH:mm","時間（分・5分単位）","コメント"};Control[] fields={project,activity,date,start,minutes,note};for(int i=0;i<fields.Length;i++){panel.Children.Add(Label(labels[i],12));panel.Children.Add(fields[i]);}
  panel.Children.Add(ButtonOf("保存",async()=>{TimeSpan t;int m;if(project.SelectedItem==null||activity.SelectedItem==null||!date.SelectedDate.HasValue||!TimeSpan.TryParse(start.Text,out t)||!int.TryParse(minutes.Text,out m)||!ValidTime(t,m)){MessageBox.Show(w,"時刻と時間は5分単位で、終了は当日24:00までに設定してください。");return;}var desired=en.Copy();desired.Project=(string)project.SelectedItem;desired.Activity=(string)activity.SelectedItem;desired.Start=date.SelectedDate.Value.Date+t;desired.Minutes=m;desired.Note=note.Text;if(!AssignIds(desired))return;RestoreEntry(en,desired);w.Close();await CommitEntry(en,before);}));w.Loaded+=(s,e)=>{note.Focus();note.SelectAll();};w.ShowDialog();
 }
}














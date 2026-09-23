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
 [DataMember] public List<PendingChange> Pending = new List<PendingChange>();
 [DataMember(EmitDefaultValue=false)] public DateTime CachedWeek;
 [DataMember] public string[] CachedProjects = Array.Empty<string>();
 [OnDeserialized] void Upgrade(StreamingContext context) {
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
  if (args.Contains("--self-test")) { CalendarRules.Tests(); PendingQueue.Tests(); SidebarTests(); KimaiServiceTests.Run().GetAwaiter().GetResult(); if (HitMode(1,12)!=1 || HitMode(6,12)!=0 || HitMode(11,12)!=2 || Snap(63)!=65 || Snap(62)!=60 || Snap(5)!=5 || RoundDelta(-6)!=-5 || ResizeDuration(1435,5,10)!=5 || ResizeDuration(540,10,-20)!=5 || !ValidTime(TimeSpan.FromHours(9)+TimeSpan.FromMinutes(5),5) || ValidTime(TimeSpan.FromHours(9)+TimeSpan.FromMinutes(1),5) || ValidTime(TimeSpan.FromHours(23)+TimeSpan.FromMinutes(55),10) || Snap(-1)!=0 || Monday(new DateTime(2026,9,20))!=new DateTime(2026,9,14)) Environment.Exit(1); return; }
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
  Title="Kimai Blocks — UIプロトタイプ"; Width=1540; Height=900; MinWidth=1240; MinHeight=650; Background=BrushOf("#F5F7FA"); FontFamily=new FontFamily("Yu Gothic UI"); FontSize=13;
  Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/KimaiBlocks.ico"));
  week=Monday(DateTime.Today); if(demo)Load(true);else {state=new State();Projects=Array.Empty<string>();file=null;}
  var root=new DockPanel { Background=Background }; Content=root;
  var top=new DockPanel { Background=BrushOf("#142B40"), LastChildFill=true, Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
  var title=Label("▦  Kimai Blocks",22); title.Foreground=Brushes.White; title.Margin=new Thickness(20,16,20,16); top.Children.Add(title);
  AddConnectionTools(top,demo);
  status.Margin=new Thickness(16,8,16,8); status.Text="一覧からドラッグして配置 • 上下端で時間変更 • ダブルクリックで編集 • Deleteで削除"; DockPanel.SetDock(status,Dock.Bottom); root.Children.Add(status);
  BuildSidebar(root);
  BuildStatistics(root);
  var main=new DockPanel { Margin=new Thickness(0,0,14,0) }; root.Children.Add(main);
  var toolbar=new DockPanel { Margin=new Thickness(0,0,0,10) }; DockPanel.SetDock(toolbar,Dock.Top); main.Children.Add(toolbar);
  var nav=new StackPanel { Orientation=Orientation.Horizontal }; nav.Children.Add(ButtonOf("‹",async()=>await ChangeWeek(week.AddDays(-7)))); nav.Children.Add(ButtonOf("今日",async()=>await ChangeWeek(Monday(DateTime.Today)))); nav.Children.Add(ButtonOf("›",async()=>await ChangeWeek(week.AddDays(7)))); period.FontSize=19; period.Margin=new Thickness(14,0,14,0); period.VerticalAlignment=VerticalAlignment.Center; var weekButton=ButtonOf("",SelectWeek);weekButton.Content=period;weekButton.Padding=new Thickness(0,4,0,4);weekButton.ToolTip="カレンダーで表示週を選択";nav.Children.Add(weekButton); toolbar.Children.Add(nav);
  var zoom=new ComboBox { ItemsSource=new string[] { "標準", "拡大" }, SelectedIndex=0, Width=66, Margin=new Thickness(4), VerticalContentAlignment=VerticalAlignment.Center }; zoom.SelectionChanged+=(s,e)=>{ double hourOffset=calendarScroll.VerticalOffset/Hour; Hour=zoom.SelectedIndex==0?144:288; board.Height=24*Hour;Render();calendarScroll.ScrollToVerticalOffset(hourOffset*Hour); }; toolbar.Children.Add(zoom);
  total.HorizontalAlignment=HorizontalAlignment.Right; total.VerticalAlignment=VerticalAlignment.Center; toolbar.Children.Add(total);
  headers.Height=52; headers.Background=Brushes.White; DockPanel.SetDock(headers,Dock.Top); main.Children.Add(headers);
  var scroll=new ScrollViewer { Content=board, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, Background=Brushes.White }; main.Children.Add(scroll); calendarScroll=scroll;
  board.Height=24*Hour; board.AllowDrop=true; board.SizeChanged+=(s,e)=>{if(!busy)Render();}; board.Drop+=DropWork;
  board.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent("work")?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
  board.MouseLeftButtonDown+=(s,e)=>{ if(e.OriginalSource==board) {selected=null;Render();} };
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Delete && selected!=null && !search.IsKeyboardFocusWithin && !projectSearch.IsKeyboardFocusWithin && !tree.IsKeyboardFocusWithin) { Delete(selected); e.Handled=true; }};
  Loaded+=(s,e)=>{Render();scroll.ScrollToVerticalOffset(8*Hour);}; Populate();
 }
 void Load(bool demo) {
  try { if(!demo && File.Exists(file)) { using(var f=File.OpenRead(file)) state=(State)new DataContractJsonSerializer(typeof(State)).ReadObject(f); if(state==null || state.Entries==null || state.Hidden==null || state.Favorites==null) throw new Exception(); return; } }
  catch { MessageBox.Show("保存データを読み込めませんでした。元ファイルを保護するため終了します。"); Environment.Exit(1); }
  state=new State(); state.Favorites.Add(Projects[0]+"|設計");
  for(int d=0;d<5;d++) { state.Entries.Add(new Entry { Project=Projects[d%4],Activity="設計",Start=week.AddDays(d).AddHours(9),Minutes=90 });state.Entries.Add(new Entry {Project=Projects[(d+1)%4],Activity="実装",Start=week.AddDays(d).AddHours(11),Minutes=120});state.Entries.Add(new Entry {Project=Projects[d%4],Activity="レビュー",Start=week.AddDays(d).AddHours(14),Minutes=60}); }
 }
 void Save() { try { Persist(); } catch(Exception ex) { status.Text="キャッシュ保存失敗: "+ex.Message;MessageBox.Show(this,status.Text,"保存失敗"); } }
 void Persist() {
  if(file==null)return;
  state.CachedWeek=week;state.CachedProjects=Projects;
  Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";
  using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(State)).WriteObject(f,state);
  if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);
 }
 int DisplayDayCount=>settings.ShowWeekends?7:5;
 double DayWidth {get{return Math.Max(1,(board.ActualWidth-Gutter)/DisplayDayCount);}}
 void Render() {
  if(busy)return; busy=true;
  try {
   board.Children.Clear(); headers.Children.Clear(); headers.ColumnDefinitions.Clear();headers.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(Gutter)});
   period.Text=week.ToString("yyyy/M/d")+" – "+week.AddDays(6).ToString("M/d");
   var entries=VisibleEntries();RenderStatistics(entries);total.Text="週合計  "+(entries.Sum(x=>x.Minutes)/60.0).ToString("0.##")+" h  ·  5分刻み";
   DrawUnavailable();
   for(int d=0;d<DisplayDayCount;d++) {headers.ColumnDefinitions.Add(new ColumnDefinition());var h=Label(week.AddDays(d).ToString("M/d (ddd)"),14);h.HorizontalAlignment=HorizontalAlignment.Center;if(week.AddDays(d)==DateTime.Today)h.Foreground=BrushOf("#1971C2");DateTime headerDay=week.AddDays(d);h.Cursor=Cursors.Hand;h.ToolTip="この日の統計を表示";h.MouseLeftButtonDown+=(s,e)=>{statisticsDay=headerDay;SetStatisticsVisible(true);statisticsTabs.SelectedIndex=1;RenderStatistics(VisibleEntries());};Grid.SetColumn(h,d+1);headers.Children.Add(h);var line=new Border{Width=1,Height=board.Height,Background=BrushOf("#E8EDF2")};Canvas.SetLeft(line,Gutter+d*DayWidth);board.Children.Add(line);}
   for(int h=0;h<24;h++) {var t=Label(h.ToString("00")+":00",11);Canvas.SetTop(t,h*Hour);board.Children.Add(t);var line=new Border {Height=1,Width=DisplayDayCount*DayWidth,Background=BrushOf("#E8EDF2")};Canvas.SetLeft(line,Gutter);Canvas.SetTop(line,h*Hour);board.Children.Add(line);}
   for(int minute=SlotMinutes;minute<1440;minute+=SlotMinutes) { if(minute%60==0)continue;var line=new Border { Height=1, Width=DisplayDayCount*DayWidth, Background=BrushOf(minute%30==0?"#DCE4ED":"#F0F3F7"), IsHitTestVisible=false }; Canvas.SetLeft(line,Gutter);Canvas.SetTop(line,minute/60.0*Hour);board.Children.Add(line); }
   foreach(var en in entries.Where(e=>(e.Start.Date-week).Days<DisplayDayCount)) DrawEntry(en,entries);
  } finally {busy=false;}
 }
 void DrawEntry(Entry en,List<Entry> entries) {
  int day=(en.Start.Date-week).Days; double top=en.Start.TimeOfDay.TotalHours*Hour;
  // Partition each day's connected overlapping intervals into reusable lanes.
  var ordered=entries.Where(x=>x.Start.Date==en.Start.Date).OrderBy(x=>x.Start).ThenBy(x=>x.Minutes).ToList();
  var group=new List<Entry>(); DateTime end=DateTime.MinValue;
  foreach(var x in ordered) {if(x.Start>=end&&group.Count>0){if(group.Contains(en))break;group.Clear();end=DateTime.MinValue;}group.Add(x);if(x.Start.AddMinutes(x.Minutes)>end)end=x.Start.AddMinutes(x.Minutes);}
  var lanes=new List<DateTime>();int lane=0;foreach(var x in group){int n=lanes.FindIndex(t=>t<=x.Start);if(n<0){n=lanes.Count;lanes.Add(DateTime.MinValue);}lanes[n]=x.Start.AddMinutes(x.Minutes);if(x==en)lane=n;}
  double width=(DayWidth-6)/Math.Max(1,lanes.Count);
  var box=new Border { Width=Math.Max(12,width-3),Height=en.Minutes/60.0*Hour,Background=BrushOf(ColorFor(en.Project)),BorderBrush=en==selected?BrushOf("#1769AA"):BrushOf("#FFFFFF"),BorderThickness=new Thickness(en==selected?2:1),CornerRadius=new CornerRadius(5),ClipToBounds=true,ToolTip=(en.ReadOnlyReason==null?"":"読み取り専用: "+en.ReadOnlyReason+"\n")+en.Project+" / "+en.Activity+"\n"+BlockTime(en)+"\n"+en.Note };
  Canvas.SetLeft(box,Gutter+day*DayWidth+3+lane*width);Canvas.SetTop(box,top);board.Children.Add(box);
  var content=new Grid();box.Child=content;
  var text=Label(BlockTime(en)+"\n"+en.Project+"\n"+en.Activity,11);text.VerticalAlignment=VerticalAlignment.Top;text.Margin=new Thickness(4,en.Minutes<=10?0:7,3,0);if(en.Minutes<=10) {text.FontSize=9;text.Text=BlockTime(en);}else text.TextWrapping=TextWrapping.Wrap;text.IsHitTestVisible=false;content.Children.Add(text);
  Entry before=en.Copy();DateTime original=en.Start;int duration=en.Minutes;Point origin=new Point();bool moved=false;int mode=0;
  box.MouseLeftButtonDown+=(s,e)=>{if(!CanEdit(en)){e.Handled=true;return;}if(e.ClickCount==2){Edit(en);e.Handled=true;return;}selected=en;statisticsDay=en.Start.Date;RenderStatistics(VisibleEntries());before=en.Copy();original=en.Start;duration=en.Minutes;origin=e.GetPosition(board);double y=e.GetPosition(box).Y;mode=HitMode(y,box.ActualHeight);moved=false;box.CaptureMouse();e.Handled=true;};
  box.MouseMove+=(s,e)=>{
   double y=e.GetPosition(box).Y;box.Cursor=(HitMode(y,box.ActualHeight)!=0)?Cursors.SizeNS:Cursors.SizeAll;
   if(!box.IsMouseCaptured||e.LeftButton!=MouseButtonState.Pressed)return;
   var p=e.GetPosition(board);if((p-origin).Length<4&&!moved)return;moved=true;
   int delta=RoundDelta((p.Y-origin.Y)/Hour*60);
   if(mode==0){int d=Math.Max(0,Math.Min(DisplayDayCount-1,(int)((p.X-Gutter)/DayWidth)));int minute=Math.Max(0,Math.Min(1440-duration,(int)original.TimeOfDay.TotalMinutes+delta));en.Start=week.AddDays(d).AddMinutes(minute);}
   else if(mode==1){int shift=Math.Max(-(int)original.TimeOfDay.TotalMinutes,Math.Min(duration-SlotMinutes,delta));en.Start=original.AddMinutes(shift);en.Minutes=duration-shift;}
   else en.Minutes=ResizeDuration((int)original.TimeOfDay.TotalMinutes,duration,delta);
   Canvas.SetTop(box,en.Start.TimeOfDay.TotalHours*Hour);Canvas.SetLeft(box,Gutter+(en.Start.Date-week).Days*DayWidth+3+lane*width);box.Height=en.Minutes/60.0*Hour;text.Text=BlockTime(en)+"\n"+en.Project+"\n"+en.Activity;
  };
  box.MouseLeftButtonUp+=async(s,e)=>{if(box.IsMouseCaptured){box.ReleaseMouseCapture();e.Handled=true;if(moved)await CommitEntry(en,before);else Render();}};
  var menu=new ContextMenu();var edit=new MenuItem{Header="編集"};edit.Click+=(s,e)=>Edit(en);menu.Items.Add(edit);var del=new MenuItem{Header="削除"};del.Click+=(s,e)=>Delete(en);menu.Items.Add(del);box.ContextMenu=menu;
 }
 async void DropWork(object sender,DragEventArgs e) {if(!e.Data.GetDataPresent("work")||!CanEdit())return;var pos=e.GetPosition(board);if(pos.X<Gutter)return;string[] key=System.Text.Json.JsonSerializer.Deserialize<string[]>((string)e.Data.GetData("work"));int d=Math.Max(0,Math.Min(DisplayDayCount-1,(int)((pos.X-Gutter)/DayWidth)));int minute=Math.Min(1440-SlotMinutes,Snap(pos.Y/Hour*60));var en=new Entry{Project=key[0],Activity=key[1],Start=week.AddDays(d).AddMinutes(minute),Minutes=Math.Min(60,1440-minute)};if(!AssignIds(en))return;state.Entries.Add(en);selected=en;Render();e.Handled=true;await CommitEntry(en,null);}
 async void Delete(Entry en) {await DeleteEntry(en);}
 void Edit(Entry en) {
  if(!CanEdit(en))return;Entry before=en.Copy();
  var w=new Window {Title="実績を編集",Width=380,Height=570,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var panel=new StackPanel {Margin=new Thickness(22)};w.Content=panel;
  var project=new ComboBox {ItemsSource=Projects,SelectedItem=en.Project};var activity=new ComboBox {ItemsSource=ActivitiesFor(en.Project),SelectedItem=en.Activity};var date=new DatePicker {SelectedDate=en.Start.Date};var start=new TextBox {Text=en.Start.ToString("HH:mm")};var minutes=new TextBox {Text=en.Minutes.ToString()};var note=new TextBox {Text=en.Note,Height=65,AcceptsReturn=true};
  project.SelectionChanged+=(s,e)=>{activity.ItemsSource=ActivitiesFor((string)project.SelectedItem);activity.SelectedIndex=0;};
  string[] labels={"プロジェクト","アクティビティ","日付（未来も入力できます）","開始時刻 HH:mm","時間（分・5分単位）","メモ"};Control[] fields={project,activity,date,start,minutes,note};for(int i=0;i<fields.Length;i++){panel.Children.Add(Label(labels[i],12));panel.Children.Add(fields[i]);}
  var billable=new CheckBox {Content="請求対象",IsChecked=en.Billable,Margin=new Thickness(4,10,4,4)};panel.Children.Add(billable);
  panel.Children.Add(ButtonOf("保存",async()=>{TimeSpan t;int m;if(project.SelectedItem==null||activity.SelectedItem==null||!date.SelectedDate.HasValue||!TimeSpan.TryParse(start.Text,out t)||!int.TryParse(minutes.Text,out m)||!ValidTime(t,m)){MessageBox.Show(w,"時刻と時間は5分単位で、終了は当日24:00までに設定してください。");return;}en.Project=(string)project.SelectedItem;en.Activity=(string)activity.SelectedItem;en.Start=date.SelectedDate.Value.Date+t;en.Minutes=m;en.Note=note.Text;en.Billable=billable.IsChecked==true;if(!AssignIds(en))return;w.Close();await CommitEntry(en,before);}));w.ShowDialog();
 }
}











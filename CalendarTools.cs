using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

public partial class Blocks {
 ToggleButton weekendToggle,dayToggle,weekToggle,lockToggle;
 DateTime? rangeStart;int rangeMinutes;Border rangeVisual;
 void AddViewTools(Panel bar) {
  bar.Children.Add(new Border {Width=1,Margin=new Thickness(10,7,5,7),Background=BrushOf("#CDD7E0")});
  ToggleButton Toggle(string text,string help,Action action){var b=new ToggleButton {Content=text,ToolTip=help,Height=30,Padding=new Thickness(9,3,9,3),Margin=new Thickness(3),Focusable=false};b.Click+=(s,e)=>action();bar.Children.Add(b);return b;}
  weekendToggle=Toggle("▦ 土日","土日の表示／非表示",ToggleWeekends);
  dayToggle=Toggle("▣ 日","選択した実績・時間範囲・日集計の日を1列で拡大表示",()=>SetDayView(true));
  weekToggle=Toggle("▥ 週","週単位で表示",()=>SetDayView(false));
  lockToggle=Toggle("入力ロック","休日・休み時間・時間外への入力を禁止／解除（既存実績は維持）",ToggleInputLock);UpdateViewTools();
 }
 void UpdateViewTools(){if(weekendToggle==null)return;weekendToggle.IsChecked=settings.ShowWeekends;dayToggle.IsChecked=dayView;weekToggle.IsChecked=!dayView;lockToggle.IsChecked=Rules.BlockInput;}
 void ToggleWeekends(){if(!ApplyEditor())return;settings.ShowWeekends=!settings.ShowWeekends;rangeStart=null;SaveViewPreferences();Render();UpdateViewTools();}
 void ToggleInputLock(){if(!ApplyEditor())return;Rules.BlockInput=!Rules.BlockInput;SaveViewPreferences();Render();UpdateViewTools();}
 void SetDayView(bool value){if(!ApplyEditor()||dragActive)return;displayDay=selected?.Start.Date??rangeStart?.Date??statisticsDay;if(displayDay<week||displayDay>=week.AddDays(7))displayDay=DateTime.Today>=week&&DateTime.Today<week.AddDays(7)?DateTime.Today:week;dayView=value;if(value){statisticsDay=displayDay;statisticsTabs.SelectedIndex=1;}rangeStart=null;pasteTime=null;Render();UpdateViewTools();}
 async Task NavigateCalendar(int delta){if(!dayView){await ChangeWeek(week.AddDays(delta*7));return;}var target=DisplayStart.AddDays(delta);if(Monday(target)!=week)await ChangeWeek(Monday(target));if(week==Monday(target)){displayDay=target;statisticsDay=target;rangeStart=null;Render();}}
 async Task NavigateToday(){await ChangeWeek(Monday(DateTime.Today));if(week==Monday(DateTime.Today)){displayDay=DateTime.Today;statisticsDay=displayDay;rangeStart=null;Render();}}
 void ViewSettingsDialog() {
  var w=new Window {Title="表示・作業ツリー",Owner=this,Width=540,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(20)};w.Content=panel;
  var weekends=new CheckBox {Content="土日を表示",IsChecked=settings.ShowWeekends,Margin=new Thickness(4,8,4,12)};panel.Children.Add(weekends);
  panel.Children.Add(Label("アクティビティの分類（正規表現、空欄で無効）",13));var pattern=new TextBox {Text=EffectiveActivityPattern,IsReadOnly=managedSettings.ActivityGroupingPattern!=null,ToolTip=managedSettings.ActivityGroupingPattern!=null?"settings.policy.jsonで管理されています":null,Margin=new Thickness(4),Padding=new Thickness(6)};panel.Children.Add(pattern);
  var help=Label("例: (.*)/(.*) → $1でまとめ、$2を子項目に表示。\n一致しない項目は元の名前で表示します。",12);help.TextWrapping=TextWrapping.Wrap;panel.Children.Add(help);
  panel.Children.Add(ButtonOf("保存",()=>{try{_ = new ActivityGrouping(pattern.Text);var previous=settings.ActivityGroupingPattern;bool old=settings.ShowWeekends;if(managedSettings.ActivityGroupingPattern==null)settings.ActivityGroupingPattern=pattern.Text;settings.ShowWeekends=weekends.IsChecked==true;try{StoreSettings();}catch{settings.ActivityGroupingPattern=previous;settings.ShowWeekends=old;throw;}rangeStart=null;Populate();Render();Save();UpdateViewTools();w.Close();}catch(Exception ex){MessageBox.Show(w,SafeError(ex),"設定を保存できません");}}));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();
 }
 void SyncSettingsDialog() {
  var w=new Window {Title="自動保存・キャッシュ・更新先",Owner=this,Width=540,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(20)};w.Content=panel;
  TextBox Field(string label,string value){panel.Children.Add(Label(label,12));var input=new TextBox {Text=value,Padding=new Thickness(6),Margin=new Thickness(4)};panel.Children.Add(input);return input;}
  var seconds=Field("保存間隔（秒、10〜3600）",settings.SaveSeconds.ToString());var minutes=Field("一覧キャッシュ（分、1〜1440）",settings.CatalogMinutes.ToString());var folder=Field("アップデート確認フォルダ（空欄で無効・UNCパス可）",EffectiveUpdateFolder);folder.IsReadOnly=managedSettings.UpdateFolder!=null;folder.ToolTip=managedSettings.UpdateFolder!=null?"settings.policy.jsonで管理されています":null;
  panel.Children.Add(ButtonOf("保存",()=>{if(!int.TryParse(seconds.Text,out var sec)||sec<10||sec>3600||!int.TryParse(minutes.Text,out var min)||min<1||min>1440){MessageBox.Show(w,"保存間隔とキャッシュ期間を範囲内で指定してください。");return;}var old=settings;try{var updated=System.Text.Json.JsonSerializer.Deserialize<ConnectionSettings>(System.Text.Json.JsonSerializer.Serialize(settings));updated.SaveSeconds=sec;updated.CatalogMinutes=min;if(managedSettings.UpdateFolder==null)updated.UpdateFolder=folder.Text.Trim();settings=updated;try{StoreSettings();}catch{settings=old;throw;}saveTimer.Interval=TimeSpan.FromSeconds(sec);w.Close();}catch(Exception ex){MessageBox.Show(w,SafeError(ex),"設定を保存できません");}}));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();
 }
 void SetupRangeSelection() {
  DateTime anchor=default;bool selecting=false;
  board.MouseLeftButtonDown+=(s,e)=>{if(e.Handled||!ApplyEditor())return;var p=e.GetPosition(board);if(p.X<Gutter)return;int d=Math.Clamp((int)((p.X-Gutter)/DayWidth),0,DisplayDayCount-1);anchor=DisplayStart.AddDays(d).AddMinutes(Math.Clamp(Snap(p.Y/Hour*60),0,1435));pasteTime=anchor;rangeStart=null;
   if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){selectedKeys.Clear();selected=null;RefreshSelection();}selecting=true;board.CaptureMouse();board.Focus();DrawSelectedRange();e.Handled=true;};
  board.MouseMove+=(s,e)=>{if(!selecting||e.LeftButton!=MouseButtonState.Pressed)return;int end=Math.Clamp(Snap(e.GetPosition(board).Y/Hour*60),0,1440);int begin=(int)anchor.TimeOfDay.TotalMinutes;if(end==begin)return;rangeStart=anchor.Date.AddMinutes(Math.Min(begin,end));rangeMinutes=Math.Max(5,Math.Abs(end-begin));pasteTime=rangeStart;DrawSelectedRange();status.Text=rangeStart.Value.ToString("M/d HH:mm")+" – "+rangeStart.Value.AddMinutes(rangeMinutes).ToString("HH:mm")+"：作業または親項目をドラッグして配置";};
  board.MouseLeftButtonUp+=(s,e)=>{if(selecting){selecting=false;board.ReleaseMouseCapture();e.Handled=true;}};board.LostMouseCapture+=(s,e)=>selecting=false;
 }
 void DrawSelectedRange(){if(rangeVisual!=null)board.Children.Remove(rangeVisual);if(!rangeStart.HasValue||rangeStart<DisplayStart||rangeStart>=DisplayStart.AddDays(DisplayDayCount))return;rangeVisual=new Border {Width=Math.Max(1,DayWidth-4),Height=rangeMinutes/60.0*Hour,Background=new SolidColorBrush(Color.FromArgb(40,30,115,210)),BorderBrush=BrushOf("#1971C2"),BorderThickness=new Thickness(1),IsHitTestVisible=false};Canvas.SetLeft(rangeVisual,Gutter+(rangeStart.Value.Date-DisplayStart).Days*DayWidth+2);Canvas.SetTop(rangeVisual,rangeStart.Value.TimeOfDay.TotalHours*Hour);Panel.SetZIndex(rangeVisual,900);board.Children.Add(rangeVisual);}
 string[] ResolveDroppedWork(IDataObject data) {
  if(data.GetDataPresent("work"))return System.Text.Json.JsonSerializer.Deserialize<string[]>((string)data.GetData("work"));
  string project=null,group=null;
  if(data.GetDataPresent("project"))project=data.GetData("project") as string;
  else if(data.GetDataPresent("work-group")){var values=System.Text.Json.JsonSerializer.Deserialize<string[]>((string)data.GetData("work-group"));project=values[0];group=values[1];}
  if(project==null)return null;
  var options=ActivitiesFor(project).Where(a=>group==null||activityGrouping.Split(a).Group==group).ToArray();
  var w=new Window {Title="配置するアクティビティを選択",Owner=this,Width=500,Height=420,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel {Margin=new Thickness(16)};w.Content=panel;var title=Label(project,13);title.TextWrapping=TextWrapping.Wrap;DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
  var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);var query=new TextBox {Margin=new Thickness(4),Padding=new Thickness(5)};DockPanel.SetDock(query,Dock.Top);panel.Children.Add(query);var list=new ListBox {ItemsSource=options,SelectedIndex=0};panel.Children.Add(list);query.TextChanged+=(s,e)=>{list.ItemsSource=options.Where(a=>Matches(a,query.Text)).ToArray();list.SelectedIndex=0;};string[] result=null;
  bottom.Children.Add(ButtonOf("配置",()=>{if(list.SelectedItem is string activity){result=new[]{project,activity};w.Close();}}));bottom.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();return result;
 }
 static bool IsWorkDrop(IDataObject data)=>data.GetDataPresent("work")||data.GetDataPresent("project")||data.GetDataPresent("work-group");
}





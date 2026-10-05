using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class Blocks {
 DateTime? customFrom,customThrough;
 double rangeDayWidth=180;
 ScrollViewer headerScroll;
 DateTime LoadedStart=>customFrom.HasValue?Monday(customFrom.Value):week;
 DateTime LoadedUntil=>customThrough.HasValue?Monday(customThrough.Value).AddDays(7):week.AddDays(7);
 DateTime StatisticsWeek=>customFrom.HasValue?Monday(statisticsDay):week;
 static bool ValidDisplayRange(DateTime from,DateTime through)=>through.Date>=from.Date&&(through.Date-from.Date).Days<31;
 void UpdateCalendarWidth() {
  if(calendarScroll==null)return;
  double width=customFrom.HasValue&&!dayView?Gutter+DisplayDayCount*rangeDayWidth:Math.Max(Gutter+40,calendarScroll.ViewportWidth);
  board.Width=width;headers.Width=width;
 }
 void SelectRange() {
  if(communicating)return;
  var w=new Window {Title="表示期間と列幅",Owner=this,Width=430,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(18)};w.Content=panel;
  panel.Children.Add(Label("開始日と終了日（両端を含めて最大31日）",13));
  var from=new DatePicker {SelectedDate=customFrom??week,Margin=new Thickness(4)};var through=new DatePicker {SelectedDate=customThrough??week.AddDays(6),Margin=new Thickness(4)};panel.Children.Add(from);panel.Children.Add(through);
  var widthLabel=Label("",12);panel.Children.Add(widthLabel);var width=new Slider {Minimum=100,Maximum=400,Value=rangeDayWidth,TickFrequency=10,IsSnapToTickEnabled=true,Margin=new Thickness(8)};width.ValueChanged+=(s,e)=>widthLabel.Text="1日の幅: "+Math.Round(width.Value)+" px";widthLabel.Text="1日の幅: "+rangeDayWidth+" px";panel.Children.Add(width);
  var hint=Label("期間表示では土日も含む連続した日付を表示します。\n収まらない日は下の横スクロールで移動できます。\n「週」ボタンで通常表示へ戻ります。",12);hint.TextWrapping=TextWrapping.Wrap;panel.Children.Add(hint);
  panel.Children.Add(ButtonOf("表示",async()=>{if(!from.SelectedDate.HasValue||!through.SelectedDate.HasValue||!ValidDisplayRange(from.SelectedDate.Value,through.SelectedDate.Value)){MessageBox.Show(w,"開始日から終了日までを1〜31日で指定してください。");return;}var begin=from.SelectedDate.Value.Date;var end=through.SelectedDate.Value.Date;double dayWidth=width.Value;w.Close();await ChangeRange(begin,end,dayWidth);}));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();
 }
 async Task ChangeRange(DateTime from,DateTime through,double width) {
  if(!ValidDisplayRange(from,through)||communicating||!ApplyEditor())return;
  if(customFrom==from.Date&&customThrough==through.Date){rangeDayWidth=Math.Clamp(width,100,400);dayView=false;Render();UpdateViewTools();Save();return;}
  if(!await FlushAsync(true))return;
  await ReadRemote(Monday(from),false,from.Date,through.Date,Math.Clamp(width,100,400));
 }
 async Task ShowPreviousWeek() {
  if(communicating||service==null||!ApplyEditor())return;
  DateTime current=customFrom.HasValue?Monday(statisticsDay):week;
  if(current<LoadedStart||current>=LoadedUntil)current=LoadedStart;
  await BeginProgress("先週の実績を読み込み中…");System.Collections.Generic.List<Entry> previous;
  try {previous=await service.ReadWeekAsync(current.AddDays(-7));}
  catch(Exception ex){MessageBox.Show(progressWindow,SafeError(ex),"先週を読み込めません");return;}
  finally {EndProgress();}
  var present=VisibleEntries().Where(e=>e.Start>=current&&e.Start<current.AddDays(7)).Select(e=>e.Copy()).ToList();
  ShowWeekComparison(current,previous,present);
 }
 void ShowWeekComparison(DateTime current,System.Collections.Generic.List<Entry> previous,System.Collections.Generic.List<Entry> present) {
  var w=new Window {Title="先週と比較",Owner=this,Width=1450,Height=850,MinWidth=1000,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner};var root=new DockPanel {Margin=new Thickness(12)};w.Content=root;
  var hint=Label("左: 先週のサーバー実績 ／ 右: 表示週の実績（未保存の編集を含む） · 縦スクロールは連動 · 左のブロックを選択してコピー（Ctrl: 複数選択 / Shift: 範囲選択）",12);hint.TextWrapping=TextWrapping.Wrap;DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
  var picked=new HashSet<Entry>();Entry anchor=null;var visuals=new Dictionary<Border,Entry>();
  var actions=new StackPanel {Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Top);root.Children.Add(actions);var feedback=Label("先週のブロックを選択してください",12);
  bool CopyPicked(){if(!CopyComparisonEntries(picked)){feedback.Text=status.Text;return false;}feedback.Text=picked.Count+"件をコピー済み。閉じてメイン画面の貼り付け位置をクリック → Ctrl+V";return true;}
  var copy=ButtonOf("コピー（Ctrl+C）",()=>CopyPicked());copy.IsEnabled=false;actions.Children.Add(copy);
  var copyBack=ButtonOf("コピーして戻る",()=>{if(CopyPicked())w.Close();});copyBack.IsEnabled=false;actions.Children.Add(copyBack);actions.Children.Add(ButtonOf("閉じる",()=>w.Close()));actions.Children.Add(feedback);
  void PaintSelection(){foreach(var pair in visuals){bool active=picked.Contains(pair.Value);pair.Key.BorderBrush=active?BrushOf("#064FA3"):System.Windows.Media.Brushes.White;pair.Key.BorderThickness=new Thickness(active?3:1);}copy.IsEnabled=copyBack.IsEnabled=picked.Count>0;feedback.Text=picked.Count+"件を選択 · "+Hours(picked.Sum(e=>e.Minutes));}
  void Pick(Entry entry){var modifiers=Keyboard.Modifiers;if(modifiers.HasFlag(ModifierKeys.Shift)&&anchor!=null){var ordered=previous.Where(e=>visuals.Values.Contains(e)).OrderBy(e=>e.Start).ToList();int a=ordered.IndexOf(anchor),b=ordered.IndexOf(entry);if(!modifiers.HasFlag(ModifierKeys.Control))picked.Clear();if(a>=0&&b>=0)foreach(var row in ordered.Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1))picked.Add(row);}else if(modifiers.HasFlag(ModifierKeys.Control)){if(!picked.Add(entry))picked.Remove(entry);anchor=entry;}else{picked.Clear();picked.Add(entry);anchor=entry;}PaintSelection();}
  w.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.C&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){e.Handled=true;if(picked.Count>0)CopyPicked();}else if(e.Key==Key.Escape){e.Handled=true;w.Close();}};
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());root.Children.Add(grid);
  ScrollViewer Make(int column,DateTime start,System.Collections.Generic.List<Entry> entries) {
   entries=entries.Where(e=>!service.Projects.Any(p=>p.Id==e.ProjectId&&p.Visible==false)&&!service.Activities.Any(a=>a.Id==e.ActivityId&&a.Visible==false)).ToList();
   var panel=new DockPanel {Margin=new Thickness(4)};Grid.SetColumn(panel,column);grid.Children.Add(panel);var heading=Label(start.ToString("M/d")+" – "+start.AddDays(6).ToString("M/d")+" · "+Hours(entries.Sum(e=>e.Minutes)),16);DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
   const double hour=90,gutter=42;var head=new Grid {Height=32,Background=BrushOf("#EAF0F7")};DockPanel.SetDock(head,Dock.Top);panel.Children.Add(head);head.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(gutter)});for(int d=0;d<7;d++){head.ColumnDefinitions.Add(new ColumnDefinition());var label=Label(start.AddDays(d).ToString("M/d ddd"),10);Grid.SetColumn(label,d+1);head.Children.Add(label);}
   var canvas=new Canvas {Height=24*hour,Background=System.Windows.Media.Brushes.White};var scroll=new ScrollViewer {Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};panel.Children.Add(scroll);
   void Draw(){foreach(var old in canvas.Children.OfType<Border>())visuals.Remove(old);canvas.Children.Clear();double dayWidth=Math.Max(1,(canvas.ActualWidth-gutter)/7);for(int h=0;h<24;h++){var label=Label(h.ToString("00")+":00",9);Canvas.SetTop(label,h*hour);canvas.Children.Add(label);var line=new Border {Height=1,Width=Math.Max(1,canvas.ActualWidth-gutter),Background=BrushOf("#DFE7EF")};Canvas.SetLeft(line,gutter);Canvas.SetTop(line,h*hour);canvas.Children.Add(line);}
    for(int d=0;d<7;d++){var date=start.AddDays(d);var line=new Border {Width=1,Height=canvas.Height,Background=BrushOf("#DFE7EF")};Canvas.SetLeft(line,gutter+d*dayWidth);canvas.Children.Add(line);var rows=entries.Where(e=>e.Start<date.AddDays(1)&&e.Start.AddMinutes(e.Minutes)>date).OrderBy(e=>e.Start).ToList();
     var laneEnds=new List<DateTime>();var lanes=new Dictionary<Entry,int>();foreach(var row in rows){int lane=laneEnds.FindIndex(end=>end<=row.Start);if(lane<0){lane=laneEnds.Count;laneEnds.Add(DateTime.MinValue);}laneEnds[lane]=row.Start.AddMinutes(row.Minutes);lanes[row]=lane;}
     foreach(var entry in rows){DateTime begin=entry.Start<date?date:entry.Start,end=entry.Start.AddMinutes(entry.Minutes)>date.AddDays(1)?date.AddDays(1):entry.Start.AddMinutes(entry.Minutes);int lane=lanes[entry];double width=(dayWidth-2)/Math.Max(1,laneEnds.Count);var box=new Border {Width=Math.Max(1,width-1),Height=Math.Max(2,(end-begin).TotalHours*hour),Background=BrushOf(ColorFor(entry.Project)),BorderBrush=System.Windows.Media.Brushes.White,BorderThickness=new Thickness(1),ClipToBounds=true,ToolTip=EntryTip(entry),Child=new TextBlock {Text=BlockCaption(entry),FontSize=10,TextWrapping=TextWrapping.Wrap,Foreground=ReadableText(ColorFor(entry.Project)),Margin=new Thickness(2)}};Canvas.SetLeft(box,gutter+d*dayWidth+lane*width);Canvas.SetTop(box,begin.TimeOfDay.TotalHours*hour);canvas.Children.Add(box);if(column==0){visuals[box]=entry;box.Tag=entry;box.Cursor=Cursors.Hand;box.MouseLeftButtonDown+=(s,e)=>{e.Handled=true;Pick(entry);};box.ToolTip=new ToolTip {Content=new TextBlock {MaxWidth=460,TextWrapping=TextWrapping.Wrap,Text=entry.Project+"\n"+entry.Activity+"\n"+entry.Start.ToString("M/d ")+BlockTime(entry)+"\n"+entry.Note+"\nクリックで選択 · Ctrl+Cでコピー"}};box.BorderBrush=picked.Contains(entry)?BrushOf("#064FA3"):System.Windows.Media.Brushes.White;box.BorderThickness=new Thickness(picked.Contains(entry)?3:1);}}
    }
   }
   canvas.SizeChanged+=(s,e)=>Draw();return scroll;
  }
  var left=Make(0,current.AddDays(-7),previous);var right=Make(1,current,present);bool syncing=false;
  void Sync(ScrollViewer source,ScrollViewer target){if(syncing)return;syncing=true;target.ScrollToVerticalOffset(source.VerticalOffset);syncing=false;}
  left.ScrollChanged+=(s,e)=>{if(e.VerticalChange!=0)Sync(left,right);};right.ScrollChanged+=(s,e)=>{if(e.VerticalChange!=0)Sync(right,left);};w.Loaded+=(s,e)=>{left.ScrollToVerticalOffset(8*90);right.ScrollToVerticalOffset(8*90);};w.ShowDialog();
 }
 bool CopyComparisonEntries(IEnumerable<Entry> source) {
  var rows=source.OrderBy(e=>e.Start).ToList();if(rows.Count==0){status.Text="先週のブロックを選択してください。";return false;}
  if(rows.Any(e=>!ValidTime(e.Start.TimeOfDay,e.Minutes))){status.Text="日をまたぐ実績・5分単位以外の実績は、この操作ではコピーできません。";return false;}
  if(rows.Any(e=>!InputAvailable(e.Copy()))){status.Text="現在利用できないプロジェクト・アクティビティが含まれています。";return false;}
  clipboardEntries=rows.Select(BlockOperations.CopyAsNew).ToList();UpdateEditToolbar();status.Text=rows.Count+"件をコピーしました。貼り付け位置をクリックし、Ctrl+Vで配置してください。";return true;
 }
}

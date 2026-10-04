using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public partial class Blocks {
 DateTime statisticsDay;
 DockPanel statisticsPanel;
 TabControl statisticsTabs;
 StackPanel weekStatistics,dayStatistics;
 static string Hours(int minutes)=>(minutes/60.0).ToString("0.##",CultureInfo.InvariantCulture)+"h";
 static string BlockTime(Entry entry)=>entry.Start.ToString("HH:mm")+"–"+entry.Start.AddMinutes(entry.Minutes).ToString("HH:mm")+"  "+Hours(entry.Minutes);
 void BuildStatistics(DockPanel root) {
  var panel=new DockPanel {Width=320,Margin=new Thickness(0,0,12,0),Background=Brushes.White};
  statisticsPanel=panel;
  var close=ButtonOf("×",()=>SetStatisticsVisible(false));close.HorizontalAlignment=HorizontalAlignment.Right;close.ToolTip="統計を閉じる（メニューから再表示）";var clockRow=new DockPanel();DockPanel.SetDock(clockRow,Dock.Top);DockPanel.SetDock(close,Dock.Right);clockRow.Children.Add(close);panel.Children.Add(clockRow);
  DockPanel.SetDock(panel,Dock.Right);root.Children.Add(panel);AddPanelResizer(root,panel,Dock.Right);BuildEditor(panel);
  var detail=ButtonOf("コメント別の詳細集計…",ShowDetailedStatistics);DockPanel.SetDock(detail,Dock.Top);panel.Children.Add(detail);
  var heading=Label("実績の統計",17);heading.Margin=new Thickness(12,12,12,8);DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
  var note=Label("未保存・未来の実績を含むブロック時間の合計\n重複時間も加算 · 時間換算は小数2桁まで",10);note.TextWrapping=TextWrapping.Wrap;note.Foreground=BrushOf("#63758A");note.Margin=new Thickness(12,8,12,12);DockPanel.SetDock(note,Dock.Bottom);panel.Children.Add(note);
  statisticsTabs=new TabControl {BorderThickness=new Thickness(0),Margin=new Thickness(6,0,6,0)};
  weekStatistics=new StackPanel {Margin=new Thickness(6)};dayStatistics=new StackPanel {Margin=new Thickness(6)};
  var weekScroll=new ScrollViewer {Content=weekStatistics,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var dayScroll=new ScrollViewer {Content=dayStatistics,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  statisticsTabs.Items.Add(new TabItem {Header="週",Content=weekScroll});
  statisticsTabs.Items.Add(new TabItem {Header="日",Content=dayScroll});
  panel.Children.Add(statisticsTabs);
 }
 void RenderStatistics(List<Entry> entries) {
  if(statisticsTabs==null)return;
  statisticsPanel.Visibility=settings.ShowStatistics?Visibility.Visible:Visibility.Collapsed;if(rightGrip!=null)rightGrip.Visibility=statisticsPanel.Visibility;
  if(!settings.ShowStatistics)return;
  if(statisticsDay<week||statisticsDay>=week.AddDays(7))statisticsDay=DateTime.Today>=week&&DateTime.Today<week.AddDays(7)?DateTime.Today:week;
  weekStatistics.Children.Clear();dayStatistics.Children.Clear();
  weekStatistics.Children.Add(Label(week.ToString("M/d")+" – "+week.AddDays(6).ToString("M/d"),13));
  AddTotal(weekStatistics,entries);
  weekStatistics.Children.Add(Label("日別",13));
  int maximum=Math.Max(1,Enumerable.Range(0,7).Max(d=>entries.Where(e=>e.Start.Date==week.AddDays(d)).Sum(e=>e.Minutes)));
  for(int d=0;d<7;d++) {
   DateTime date=week.AddDays(d);int minutes=entries.Where(e=>e.Start.Date==date).Sum(e=>e.Minutes);
   var button=ButtonOf(date.ToString("M/d (ddd)")+"   "+Hours(minutes),()=>{statisticsDay=date;statisticsTabs.SelectedIndex=1;RenderStatistics(VisibleEntries());});
   button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(6,4,6,4);weekStatistics.Children.Add(button);
   weekStatistics.Children.Add(new ProgressBar {Minimum=0,Maximum=maximum,Value=minutes,Height=3,Foreground=BrushOf("#4F92CA"),Background=BrushOf("#EDF2F7"),Margin=new Thickness(4,0,4,3)});
  }

  var dayPicker=new ComboBox {Margin=new Thickness(4,6,4,8),Padding=new Thickness(6),ItemsSource=Enumerable.Range(0,7).Select(d=>week.AddDays(d).ToString("M/d (ddd)")).ToArray(),SelectedIndex=(statisticsDay-week).Days};
  dayPicker.SelectionChanged+=(s,e)=>{if(dayPicker.SelectedIndex<0)return;statisticsDay=week.AddDays(dayPicker.SelectedIndex);RenderStatistics(VisibleEntries());};
  dayStatistics.Children.Add(Label("日集計",16));dayStatistics.Children.Add(dayPicker);
  var dayEntries=entries.Where(e=>e.Start.Date==statisticsDay).ToList();AddTotal(dayStatistics,dayEntries);
  AddDailyBreakdown(dayEntries);

 }
 readonly HashSet<string> collapsedDailyNodes=new HashSet<string>();
 void AddDailyBreakdown(List<Entry> entries) {
  var tree=new TreeView {BorderThickness=new Thickness(0),Margin=new Thickness(0,8,0,8)};
  TreeViewItem Node(string name,List<Entry> rows,string key,bool expanded) {
   var label=Label(name+" · "+Hours(rows.Sum(e=>e.Minutes))+" ("+rows.Count+"件)",12);label.ToolTip=name;
   var item=new TreeViewItem {Header=label,IsExpanded=expanded&&!collapsedDailyNodes.Contains(key)};
   item.Collapsed+=(s,e)=>{if(e.OriginalSource==item)collapsedDailyNodes.Add(key);};
   item.Expanded+=(s,e)=>{if(e.OriginalSource==item)collapsedDailyNodes.Remove(key);};return item;
  }
  void Comments(ItemsControl parent,List<Entry> rows,int depth,string key) {
   foreach(var group in rows.GroupBy(e=>{var parts=EditingModel.Path(CommentStatistics.Key(e));return depth<parts.Length?parts[depth]:"（この階層のコメント／コメントなし）";})) {
    var values=group.ToList();string childKey=key+"/"+group.Key.Length+":"+group.Key;
    var node=Node(group.Key,values,childKey,true);parent.Items.Add(node);
    if(values.Any(e=>EditingModel.Path(CommentStatistics.Key(e)).Length>depth+1))Comments(node,values,depth+1,childKey);
    else foreach(var activity in values.GroupBy(e=>e.Activity)) {
     var activityNode=Node(activity.Key??"未設定",activity.ToList(),childKey+"/activity:"+activity.Key,false);node.Items.Add(activityNode);
     foreach(var row in activity.OrderBy(e=>e.Start))activityNode.Items.Add(new TreeViewItem {Header=Label(BlockTime(row),11),ToolTip=row.Note});
    }
   }
  }
  foreach(var group in entries.GroupBy(e=>e.Project).OrderByDescending(g=>g.Sum(e=>e.Minutes))) {
   var rows=group.ToList();string key=statisticsDay.ToString("yyyyMMdd")+"/"+group.Key;
   var node=Node(group.Key??"未設定",rows,key,true);tree.Items.Add(node);Comments(node,rows,0,key);
  }
  if(entries.Count==0)dayStatistics.Children.Add(Label("この日の実績はありません",12));else dayStatistics.Children.Add(tree);
 }
 void SetStatisticsVisible(bool visible) {
  settings.ShowStatistics=visible;
  statisticsPanel.Visibility=visible?Visibility.Visible:Visibility.Collapsed;if(rightGrip!=null)rightGrip.Visibility=statisticsPanel.Visibility;
  if(visible)RenderStatistics(VisibleEntries());
  try {StoreSettings();}catch(Exception ex){MessageBox.Show(this,SafeError(ex),"表示設定を保存できません");}
 }
 void SelectWeek() {
  if(communicating)return;
  var w=new Window {Title="表示週を選択",Owner=this,Width=350,Height=365,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel {Margin=new Thickness(18)};w.Content=panel;
  panel.Children.Add(Label("表示したい週の日付を選択してください",13));
  var calendar=new System.Windows.Controls.Calendar {SelectedDate=week,DisplayDate=week,FirstDayOfWeek=DayOfWeek.Monday,SelectionMode=CalendarSelectionMode.SingleDate};panel.Children.Add(calendar);
  var range=Label("",12);panel.Children.Add(range);
  Action update=()=>{var monday=Monday(calendar.SelectedDate??week);range.Text=monday.ToString("yyyy/M/d")+" – "+monday.AddDays(6).ToString("M/d");};calendar.SelectedDatesChanged+=(s,e)=>update();update();
  panel.Children.Add(ButtonOf("この週を表示",async()=>{DateTime target=Monday(calendar.SelectedDate??week);w.Close();await ChangeWeek(target);}));w.ShowDialog();
 }
 void AddTotal(StackPanel panel,List<Entry> entries) {
  var value=Label(Hours(entries.Sum(e=>e.Minutes)),30);value.FontWeight=FontWeights.SemiBold;value.Foreground=BrushOf("#1971C2");panel.Children.Add(value);
  panel.Children.Add(Label(entries.Count+" ブロック · "+entries.Sum(e=>e.Minutes)+" 分",11));
 }
}




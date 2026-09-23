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
  var panel=new DockPanel {Width=250,Margin=new Thickness(0,0,12,0),Background=Brushes.White};
  statisticsPanel=panel;
  var close=ButtonOf("×",()=>SetStatisticsVisible(false));close.HorizontalAlignment=HorizontalAlignment.Right;close.ToolTip="統計を閉じる（メニューから再表示）";DockPanel.SetDock(close,Dock.Top);panel.Children.Add(close);
  DockPanel.SetDock(panel,Dock.Right);root.Children.Add(panel);
  var heading=Label("実績の統計",17);heading.Margin=new Thickness(12,12,12,8);DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
  var note=Label("未保存・未来の実績を含むブロック時間の合計\n重複時間も加算 · 時間換算は小数2桁まで",10);note.TextWrapping=TextWrapping.Wrap;note.Foreground=BrushOf("#63758A");note.Margin=new Thickness(12,8,12,12);DockPanel.SetDock(note,Dock.Bottom);panel.Children.Add(note);
  statisticsTabs=new TabControl {BorderThickness=new Thickness(0),Margin=new Thickness(6,0,6,0)};
  weekStatistics=new StackPanel {Margin=new Thickness(6)};dayStatistics=new StackPanel {Margin=new Thickness(6)};
  statisticsTabs.Items.Add(new TabItem {Header="週",Content=new ScrollViewer {Content=weekStatistics,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});
  statisticsTabs.Items.Add(new TabItem {Header="日",Content=new ScrollViewer {Content=dayStatistics,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});
  panel.Children.Add(statisticsTabs);
 }
 void RenderStatistics(List<Entry> entries) {
  if(statisticsTabs==null)return;
  statisticsPanel.Visibility=settings.ShowStatistics?Visibility.Visible:Visibility.Collapsed;
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
  AddBreakdown(weekStatistics,"プロジェクト別",entries,e=>e.Project,true);
  var dayPicker=new ComboBox {Margin=new Thickness(4,6,4,8),Padding=new Thickness(6),ItemsSource=Enumerable.Range(0,7).Select(d=>week.AddDays(d).ToString("M/d (ddd)")).ToArray(),SelectedIndex=(statisticsDay-week).Days};
  dayPicker.SelectionChanged+=(s,e)=>{if(dayPicker.SelectedIndex<0)return;statisticsDay=week.AddDays(dayPicker.SelectedIndex);RenderStatistics(VisibleEntries());};
  dayStatistics.Children.Add(dayPicker);
  var dayEntries=entries.Where(e=>e.Start.Date==statisticsDay).ToList();AddTotal(dayStatistics,dayEntries);
  AddBreakdown(dayStatistics,"プロジェクト別",dayEntries,e=>e.Project,true);
  AddBreakdown(dayStatistics,"アクティビティ別",dayEntries,e=>e.Activity,false);
 }
 void SetStatisticsVisible(bool visible) {
  settings.ShowStatistics=visible;
  statisticsPanel.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
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
 void AddBreakdown(StackPanel panel,string title,List<Entry> entries,Func<Entry,string> key,bool project) {
  var heading=Label(title,13);heading.Margin=new Thickness(4,16,4,6);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
  if(entries.Count==0){panel.Children.Add(Label("実績なし",12));return;}
  int totalMinutes=entries.Sum(e=>e.Minutes);
  foreach(var group in entries.GroupBy(key).OrderByDescending(g=>g.Sum(e=>e.Minutes)).ThenBy(g=>g.Key)) {
   int minutes=group.Sum(e=>e.Minutes);
   var row=new DockPanel {Margin=new Thickness(4,5,4,2)};
   var amount=Label(Hours(minutes),12);amount.Margin=new Thickness(6,0,0,0);DockPanel.SetDock(amount,Dock.Right);row.Children.Add(amount);
   var name=Label(group.Key??"未設定",12);name.Margin=new Thickness(0);name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=group.Key;row.Children.Add(name);panel.Children.Add(row);
   panel.Children.Add(new ProgressBar {Minimum=0,Maximum=Math.Max(1,totalMinutes),Value=minutes,Height=4,Foreground=project?BrushOf(ColorFor(group.Key)):BrushOf("#4F92CA"),Background=BrushOf("#EDF2F7"),Margin=new Thickness(4,0,4,5)});
  }
 }
}

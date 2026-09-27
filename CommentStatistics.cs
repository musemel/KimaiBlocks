using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public static class CommentStatistics {
 public static string Key(Entry entry)=>(entry.Note??"").Replace("\r\n","\n").Replace('\r','\n').Trim();
 public static List<IGrouping<string,Entry>> Groups(IEnumerable<Entry> entries)=>entries.GroupBy(Key,StringComparer.Ordinal).OrderByDescending(g=>g.Sum(e=>e.Minutes)).ThenBy(g=>g.Key,StringComparer.Ordinal).ToList();
 public static void Tests() {
  var entries=new[]{new Entry {Note=" A\r\nB ",Minutes=10,Project="P1"},new Entry {Note="A\nB",Minutes=20,Project="P2"},new Entry {Note=null,Minutes=5},new Entry {Note="  ",Minutes=15},new Entry {Note="a\nB",Minutes=5}};
  var groups=Groups(entries);if(groups.Count!=3||groups[0].Sum(e=>e.Minutes)!=30||groups.Single(g=>g.Key=="").Sum(e=>e.Minutes)!=20||groups.Sum(g=>g.Sum(e=>e.Minutes))!=55)throw new Exception("Comment grouping lost or merged records");
 }
}
public partial class Blocks {
 void ShowDetailedStatistics() {
  if(communicating)return;
  var w=new Window {Title="コメント別の詳細集計",Owner=this,Width=880,Height=720,MinWidth=560,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var root=new DockPanel {Margin=new Thickness(18)};w.Content=root;
  var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
  top.Children.Add(Label(week.ToString("yyyy/M/d")+" – "+week.AddDays(6).ToString("M/d"),17));
  var controls=new WrapPanel();top.Children.Add(controls);
  var scope=new ComboBox {ItemsSource=new[]{"週全体","日別"},SelectedIndex=statisticsTabs?.SelectedIndex==1?1:0,Width=110,Margin=new Thickness(4),Padding=new Thickness(6)};controls.Children.Add(scope);
  int index=Math.Clamp((statisticsDay-week).Days,0,6);
  var day=new ComboBox {ItemsSource=Enumerable.Range(0,7).Select(d=>week.AddDays(d).ToString("M/d (ddd)")).ToArray(),SelectedIndex=index,Width=150,Margin=new Thickness(4),Padding=new Thickness(6)};controls.Children.Add(day);
  var grouping=new ComboBox {ItemsSource=new[]{"コメント別","プロジェクト別"},SelectedIndex=0,Width=150,Margin=new Thickness(4),Padding=new Thickness(6)};controls.Children.Add(grouping);
  var totalLabel=Label("",17);top.Children.Add(totalLabel);
  var note=Label("コメント別／プロジェクト別を選び、内訳をツリーで展開できます。\n未保存・未来・非表示曜日の実績も含み、重複時間も加算します。\n前後の空白と改行コードを除き、コメントが一致する実績をまとめます。",12);note.TextWrapping=TextWrapping.Wrap;top.Children.Add(note);
  var close=ButtonOf("閉じる",()=>w.Close());DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
  var tree=new TreeView {Margin=new Thickness(4,12,4,4)};VirtualizingPanel.SetIsVirtualizing(tree,true);VirtualizingPanel.SetVirtualizationMode(tree,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(tree,true);root.Children.Add(tree);
  TreeViewItem Node(string name,List<Entry> rows,Func<IEnumerable<TreeViewItem>> children) {
   var title=Label(name+"   ·   "+Hours(rows.Sum(e=>e.Minutes))+"  ("+rows.Count+"件)",13);title.TextWrapping=TextWrapping.Wrap;title.MaxWidth=650;title.ToolTip=name;
   var node=new TreeViewItem {Header=title};node.Items.Add(new TreeViewItem());bool loaded=false;
   node.Expanded+=(s,e)=>{if(e.OriginalSource!=node||loaded)return;loaded=true;node.Items.Clear();foreach(var child in children())node.Items.Add(child);};return node;
  }
  IEnumerable<TreeViewItem> Records(List<Entry> rows)=>rows.OrderBy(e=>e.Start).Select(e=>new TreeViewItem {Header=Label(e.Start.ToString("M/d ")+BlockTime(e),12),ToolTip=e.Note});
  IEnumerable<TreeViewItem> Activities(List<Entry> rows)=>rows.GroupBy(e=>e.Activity).OrderByDescending(g=>g.Sum(e=>e.Minutes)).Select(g=>Node(g.Key??"未設定",g.ToList(),()=>Records(g.ToList())));
  IEnumerable<TreeViewItem> ProjectsIn(List<Entry> rows)=>rows.GroupBy(e=>e.Project).OrderByDescending(g=>g.Sum(e=>e.Minutes)).Select(g=>Node(g.Key??"未設定",g.ToList(),()=>Activities(g.ToList())));
  IEnumerable<TreeViewItem> CommentsIn(List<Entry> rows)=>CommentStatistics.Groups(rows).Select(g=>Node(g.Key.Length==0?"（コメントなし）":g.Key,g.ToList(),()=>Records(g.ToList())));
  IEnumerable<TreeViewItem> ActivitiesWithComments(List<Entry> rows)=>rows.GroupBy(e=>e.Activity).OrderByDescending(g=>g.Sum(e=>e.Minutes)).Select(g=>Node(g.Key??"未設定",g.ToList(),()=>CommentsIn(g.ToList())));
  void Refresh() {
   day.IsEnabled=scope.SelectedIndex==1;var entries=VisibleEntries();if(scope.SelectedIndex==1)entries=entries.Where(e=>e.Start.Date==week.AddDays(Math.Max(0,day.SelectedIndex))).ToList();
   totalLabel.Text="合計 "+Hours(entries.Sum(e=>e.Minutes))+"  ·  "+entries.Count+"ブロック";tree.Items.Clear();
   if(grouping.SelectedIndex==1){foreach(var group in entries.GroupBy(e=>e.Project).OrderByDescending(g=>g.Sum(e=>e.Minutes))){var rows=group.ToList();tree.Items.Add(Node(group.Key??"未設定",rows,()=>ActivitiesWithComments(rows)));}}
   else foreach(var group in CommentStatistics.Groups(entries)){var rows=group.ToList();tree.Items.Add(Node(group.Key.Length==0?"（コメントなし）":group.Key,rows,()=>ProjectsIn(rows)));}
   if(entries.Count==0)tree.Items.Add(new TreeViewItem {Header="実績なし"});
  }
  grouping.SelectionChanged+=(s,e)=>Refresh();
  scope.SelectionChanged+=(s,e)=>Refresh();day.SelectionChanged+=(s,e)=>Refresh();Refresh();w.ShowDialog();
 }
}

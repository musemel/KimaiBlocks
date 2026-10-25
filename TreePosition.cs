using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public partial class Blocks {
 sealed class TreePosition {
  public double Offset,Horizontal;
  public List<(string Key,double Y)> Anchors=new();
 }
 static IEnumerable<T> TreeVisuals<T>(DependencyObject parent) where T:DependencyObject {
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) {
   var child=VisualTreeHelper.GetChild(parent,i);if(child is T found)yield return found;
   foreach(var descendant in TreeVisuals<T>(child))yield return descendant;
  }
 }
 IEnumerable<(TreeViewItem Node,string Key)> VisibleTreeNodes(ItemsControl parent,string path="") {
  foreach(var node in parent.Items.OfType<TreeViewItem>()) {
   string key=path+"/"+(node.Tag as string??"");yield return (node,key);
   if(node.IsExpanded)foreach(var child in VisibleTreeNodes(node,key))yield return child;
  }
 }
 TreePosition CaptureTreePosition() {
  var scroll=TreeVisuals<ScrollViewer>(tree).FirstOrDefault();if(scroll==null||!tree.IsVisible)return null;
  var position=new TreePosition {Offset=scroll.VerticalOffset,Horizontal=scroll.HorizontalOffset};
  foreach(var row in VisibleTreeNodes(tree)) {
   if(row.Node.Header is not FrameworkElement header||!header.IsVisible||header.ActualHeight<=0)continue;
   double y=header.TranslatePoint(new Point(),tree).Y;
   if(y+header.ActualHeight>0&&y<tree.ActualHeight)position.Anchors.Add((row.Key,y));
  }
  return position;
 }
 void RestoreTreePosition(TreePosition position) {
  if(position==null)return;
  tree.UpdateLayout();var scroll=TreeVisuals<ScrollViewer>(tree).FirstOrDefault();if(scroll==null)return;
  var rows=VisibleTreeNodes(tree).ToList();
  foreach(var anchor in position.Anchors) {
   var node=rows.FirstOrDefault(r=>r.Key==anchor.Key).Node;if(node?.Header is not FrameworkElement header)continue;
   node.BringIntoView();tree.UpdateLayout();
   double y=header.TranslatePoint(new Point(),tree).Y;
   scroll.ScrollToVerticalOffset(Math.Max(0,scroll.VerticalOffset+y-anchor.Y));scroll.ScrollToHorizontalOffset(position.Horizontal);tree.UpdateLayout();return;
  }
  scroll.ScrollToVerticalOffset(position.Offset);scroll.ScrollToHorizontalOffset(position.Horizontal);
 }
}

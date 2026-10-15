using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

public partial class Blocks {
 static bool? ShowDialogLayout(Window window) {
  var originalScroll=window.Content as ScrollViewer;
  var form=window.Content as StackPanel??originalScroll?.Content as StackPanel;
  if(form!=null){
   var actions=form.Children.Cast<UIElement>().Reverse().TakeWhile(c=>c is Button).Reverse().ToArray();
   foreach(var button in actions)form.Children.Remove(button);
   var margin=form.Margin;form.Margin=new Thickness(0);window.Content=null;if(originalScroll!=null)originalScroll.Content=null;
   var root=new DockPanel {Margin=margin};var footer=new WrapPanel {Margin=new Thickness(0,8,0,0)};
   footer.SetBinding(UIElement.IsEnabledProperty,new Binding("IsEnabled"){Source=form});
   foreach(var button in actions)footer.Children.Add(button);
   if(actions.Length>0){DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);}
   root.Children.Add(new ScrollViewer {Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   window.Content=root;window.Height=double.NaN;window.SizeToContent=SizeToContent.Height;
   window.MaxHeight=Math.Max(240,SystemParameters.WorkArea.Height-32);
   window.MinHeight=Math.Min(180,window.MaxHeight);window.MinWidth=Math.Min(window.Width,360);
  }
  return window.ShowDialog();
 }
 static void LimitDialogHeader(Window window,DockPanel root,FrameworkElement header){
  int index=root.Children.IndexOf(header);root.Children.Remove(header);
  var scroll=new ScrollViewer {Content=header,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=window.Height*.55};
  DockPanel.SetDock(scroll,Dock.Top);root.Children.Insert(index,scroll);
  window.SizeChanged+=(s,e)=>scroll.MaxHeight=Math.Max(120,(window.ActualHeight-48)*.55);
 }
}

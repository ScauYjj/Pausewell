using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Pausewell.Controls;
using static Pausewell.PageLab.PageUi;
namespace Pausewell.PageLab;
public sealed class TimeFields:StackPanel
{
 private readonly List<string> _cache=[];
 private readonly WrapPanel _choices=new(),_fields=new();
 private readonly TextBlock _summary=Text("",12,color:"Color.textSecondary",margin:new(0,8,0,12));
 private readonly string _prefix;
 public List<TextBox> Inputs {get;}=[];
 public int Count=>Inputs.Count;
 public event Action? Changed;
 public TimeFields(IEnumerable<string> initial,string prefix)
 {
  _prefix=prefix;_fields.Margin=new(0,8,0,0);_cache.AddRange(initial);Children.Add(_choices);Children.Add(_summary);Children.Add(_fields);SetCount(Math.Max(1,_cache.Count));
 }
 public void SetCount(int count)
 {
  if(count<1||count>24)return;for(var i=0;i<Inputs.Count;i++)_cache[i]=Inputs[i].Text;while(_cache.Count<count)_cache.Add("");Inputs.Clear();_fields.Children.Clear();_choices.Children.Clear();
  for(var i=1;i<=4;i++){var requested=i;var radio=new RadioButton{Content=i+" 次",GroupName=_prefix+"Count",IsChecked=count==i,Margin=new(0,0,8,0)};Id(radio,_prefix+"Count"+i,i+" 次");radio.Checked+=(_,_)=>SetCount(requested);_choices.Children.Add(radio);}
  var add=Button("＋ 增加时刻",_prefix+"AddTime",()=>SetCount(Count+1));add.IsEnabled=count<24;_choices.Children.Add(add);
  _summary.Text=$"当前 {count} 个时刻，未使用的草稿增加次数后可恢复。";_summary.Visibility=_cache.Count>count||count>4?Visibility.Visible:Visibility.Collapsed;
  for(var i=0;i<count;i++){var index=i;var input=Input(_cache[i],_prefix+"Time"+i,$"第 {i+1} 次时刻 HH:mm");input.HorizontalAlignment=HorizontalAlignment.Stretch;Inputs.Add(input);input.TextChanged+=(_,_)=>Changed?.Invoke();var group=new StackPanel{Width=192,Margin=new(0,0,16,0)};var title=new DockPanel();var remove=Button("×",_prefix+"Remove"+i,()=>Remove(index));remove.Width=32;remove.Height=32;remove.MinHeight=32;remove.Padding=new(0);remove.ToolTip=$"移除第 {i+1} 个时刻";Id(remove,_prefix+"Remove"+i,$"移除第 {i+1} 个时刻");remove.IsEnabled=count>1;DockPanel.SetDock(remove,Dock.Right);title.Children.Add(remove);title.Children.Add(Text($"第 {i+1} 次 · HH:mm",12,bold:true,margin:new(0,8,0,0)));group.Children.Add(title);group.Children.Add(Field("",input));_fields.Children.Add(group);}
  Changed?.Invoke();
 }
 private void Remove(int index){for(var i=0;i<Inputs.Count;i++)_cache[i]=Inputs[i].Text;_cache.RemoveAt(index);Inputs.Clear();SetCount(Math.Max(1,_fields.Children.Count-1));}
 public List<string> Values()=>Inputs.Select(x=>x.Text.Trim()).ToList();
 public bool Validate(bool show)
 {
  var valid=true;var values=Values();for(var i=0;i<Inputs.Count;i++){var error=!TimeOnly.TryParseExact(values[i],"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)?"请输入有效时刻，如 08:00。":values.Count(v=>v==values[i])>1?"时刻重复，请分别填写。":"";valid&=error=="";if(show)FieldState.SetError(Inputs[i],error);}return valid;
 }
}

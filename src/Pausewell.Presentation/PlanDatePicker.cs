using System.Windows.Input;
using Pausewell.Controls;
namespace Pausewell.Presentation;
// Date parsing belongs to the shared picker; paired-field constraints belong to the form.
public sealed class PlanDatePicker:IsoDatePicker
{
 public Func<string?>? AdditionalError {get;set;}
 public PlanDatePicker(){SetResourceReference(StyleProperty,"DateFieldStyle");GotKeyboardFocus+=(_,_)=>ApplyAdditionalError();LostKeyboardFocus+=(_,_)=>ApplyAdditionalError();}
 private void ApplyAdditionalError(){var error=AdditionalError?.Invoke();if(!string.IsNullOrEmpty(error))FieldState.SetError(this,error);}
}

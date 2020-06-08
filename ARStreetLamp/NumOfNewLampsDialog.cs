using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace ARStreetLamp
{
    [Activity(Label = "NumOfNewLampsDialog")]
    public class NumOfNewLampsDialog : DialogFragment
    {
        private readonly Context _context;
        private readonly int _min, _max, _current;
        public NumberPicker.IOnValueChangeListener _listener;

        protected NumOfNewLampsDialog(Context context, int current, NumberPicker.IOnValueChangeListener listener, int min = 1, int max = 50)
        {
            _context = context;
            _min = min;
            _max = max;
            _current = current;
            _listener = listener;
        }

        public override Dialog OnCreateDialog(Bundle savedState)
        {
            var inflater = (LayoutInflater)_context.GetSystemService(Context.LayoutInflaterService);
            var view = inflater.Inflate(Resource.Layout.NumOfNewLampsDIalog, null);
            var numberPicker = view.FindViewById<NumberPicker>(Resource.Id.numberPicker);
            numberPicker.MaxValue = _max;
            numberPicker.MinValue = _min;
            numberPicker.Value = _current;
            numberPicker.SetOnValueChangedListener(_listener);

            var dialog = new AlertDialog.Builder(_context);
            dialog.SetTitle("Number of new lamps:");
            dialog.SetView(view);
            dialog.SetNegativeButton("Cancel", (s, a) => { });
            dialog.SetPositiveButton("OK", (s, a) => { });
            return dialog.Create();
        }
    }
}
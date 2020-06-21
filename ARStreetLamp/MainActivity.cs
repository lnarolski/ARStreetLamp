using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Urho.Droid;
using System;
using Android.Support.V4.Content;
using Android.Support.V4.App;
using Android;
using Android.Content.PM;
using Android.Util;
using Android.Views;
using Android.Content.Res;
using System.IO;
using Java.Nio.FileNio;
using System.Collections.Generic;
using Java.Lang;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;

namespace ARStreetLamp
{
    [Activity(Label = "ARStreetLamp", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NumberPicker.IOnValueChangeListener
    {
        bool launched;
        public ARRender arrender;
        RelativeLayout placeholder;
        UrhoSurfacePlaceholder surface;
        ToggleButton hudButton;

        private int hudScreenNum = 0;
        private int lastHudScreenNum = 2;
        private Toast toast;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            placeholder = FindViewById<RelativeLayout>(Resource.Id.UrhoSurfacePlaceHolder);

            hudButton = FindViewById<ToggleButton>(Resource.Id.hudButton);
            hudButton.Click += (o, e) =>
            {
                if (hudButton.Checked)
                    ChangeVisibilityOfHUD(true);
                else
                    ChangeVisibilityOfHUD(false);
            };

            Button prevControlsScreenButton = FindViewById<Button>(Resource.Id.prevControlsScreenButton);
            Button nextControlsScreenButton = FindViewById<Button>(Resource.Id.nextControlsScreenButton);
            prevControlsScreenButton.Click += PrevControlsScreenButton_Click;
            nextControlsScreenButton.Click += NextControlsScreenButton_Click;

            LaunchUrho();

            SeekBar heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            SeekBar rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            FrameLayout rotateSeekBarFL = FindViewById<FrameLayout>(Resource.Id.rotateSeekBarFL);
            FrameLayout heightSeekBarFL = FindViewById<FrameLayout>(Resource.Id.heightSeekBarFL);

            var displayMetrics = Resources.DisplayMetrics;
            int height = displayMetrics.HeightPixels;
            int width = displayMetrics.WidthPixels;

            rotateSeekBar.LayoutParameters.Width = (int)(width * 0.9);
            rotateSeekBar.LayoutParameters.Height = (int)(height * 0.3);
            rotateSeekBarFL.SetPadding((int)(width * 0.05), (int)(height * 0.7), 0, 0);

            heightSeekBar.LayoutParameters.Width = (int)(width * 1.0);
            heightSeekBar.LayoutParameters.Height = (int)(height * 0.2);
        }

        private async System.Threading.Tasks.Task LaunchUrho()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Camera }, 42);
                return;
            }

            if (launched)
                return;

            string[] lampModels = new string[0];
            string[] poleModels = new string[0];

            AssetManager assets = this.Assets;
            using (StreamReader sr = new StreamReader(assets.Open("ArData/models.txt")))
            {
                try
                {
                    lampModels = sr.ReadLine().Split(',');
                }
                catch (Java.Lang.Exception) { }

                try
                {
                    poleModels = sr.ReadLine().Split(',');
                }
                catch (Java.Lang.Exception) { }
            }

            var paths = new string[lampModels.Length + poleModels.Length];
            lampModels.CopyTo(paths, 0);
            poleModels.CopyTo(paths, lampModels.Length);

            launched = true;
            surface = UrhoSurface.CreateSurface(this);
            placeholder.AddView(surface);
            arrender = await surface.Show<ARRender>(
                new Urho.ApplicationOptions
                {
                    ResourcePaths = new string[] { "ArData" }
                });

            arrender.poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            arrender.lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            arrender.heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            arrender.rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            arrender.prevControlsScreenButton = FindViewById<Button>(Resource.Id.prevControlsScreenButton);
            arrender.nextControlsScreenButton = FindViewById<Button>(Resource.Id.nextControlsScreenButton);
            arrender.addLampButton = FindViewById<Button>(Resource.Id.addLampButton);
            arrender.createInstalationButton = FindViewById<Button>(Resource.Id.createInstalationButton);
            arrender.deleteAllLampsButton = FindViewById<Button>(Resource.Id.deleteAllLampsButton);
            arrender.editLampButton = FindViewById<Button>(Resource.Id.editLampsButton);
            arrender.prevSelLampButton = FindViewById<Button>(Resource.Id.prevSelLampButton);
            arrender.nextSelLampButton = FindViewById<Button>(Resource.Id.nextSelLampButton);
            arrender.editLampsButton = FindViewById<Button>(Resource.Id.editLampsButton);

            arrender.mainActivity = this;
            arrender.poleModelsString = poleModels;
            arrender.lampModelsString = lampModels;
            arrender.assetManager = Assets;

            arrender.PrepareAR();

            SetTitle(Resource.String.app_name);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void NextControlsScreenButton_Click(object sender, EventArgs e)
        {
            if (hudScreenNum == lastHudScreenNum)
                hudScreenNum = 0;
            else
                ++hudScreenNum;

            SetHud(hudScreenNum);
        }

        public void PrevControlsScreenButton_Click(object sender, EventArgs e)
        {
            if (hudScreenNum == 0)
                hudScreenNum = lastHudScreenNum;
            else
                --hudScreenNum;

            SetHud(hudScreenNum);
        }

        private void SetHud(int hudScreenNum)
        {
            ToggleButton poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            ToggleButton lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            SeekBar heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            SeekBar rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            Button addLampButton = FindViewById<Button>(Resource.Id.addLampButton);
            Button createInstalationButton = FindViewById<Button>(Resource.Id.createInstalationButton);
            Button deleteAllLampsButton = FindViewById<Button>(Resource.Id.deleteAllLampsButton);
            Button editLampsButton = FindViewById<Button>(Resource.Id.editLampsButton);
            Button prevSelLampButton = FindViewById<Button>(Resource.Id.prevSelLampButton);
            Button nextSelLampButton = FindViewById<Button>(Resource.Id.nextSelLampButton);

            poleButton.Visibility = ViewStates.Invisible;
            lightButton.Visibility = ViewStates.Invisible;
            heightSeekBar.Visibility = ViewStates.Invisible;
            rotateSeekBar.Visibility = ViewStates.Invisible;
            editLampsButton.Visibility = ViewStates.Invisible;
            createInstalationButton.Visibility = ViewStates.Invisible;
            deleteAllLampsButton.Visibility = ViewStates.Invisible;
            addLampButton.Visibility = ViewStates.Invisible;
            prevSelLampButton.Visibility = ViewStates.Invisible;
            nextSelLampButton.Visibility = ViewStates.Invisible;

            switch (hudScreenNum)
            {
                case 0:
                    poleButton.Visibility = ViewStates.Visible;
                    lightButton.Visibility = ViewStates.Visible;
                    heightSeekBar.Visibility = ViewStates.Visible;
                    rotateSeekBar.Visibility = ViewStates.Visible;
                    //ShowToast("Main HUD");
                    break;
                case 1:
                    editLampsButton.Visibility = ViewStates.Visible;
                    createInstalationButton.Visibility = ViewStates.Visible;
                    deleteAllLampsButton.Visibility = ViewStates.Visible;
                    //ShowToast("Edit lamps HUD");
                    break;
                case 2:
                    addLampButton.Visibility = ViewStates.Visible;
                    prevSelLampButton.Visibility = ViewStates.Visible;
                    nextSelLampButton.Visibility = ViewStates.Visible;
                    //ShowToast("Edit lamp HUD");
                    break;
                default:
                    break;
            }
        }

        private void ShowToast(string mssg)
        {
            if (toast != null)
            {
                toast.Cancel();
            }
            toast = Toast.MakeText(Android.App.Application.Context, mssg, ToastLength.Short);
            toast.Show();
        }

        protected override void OnResume()
        {
            base.OnResume();
            UrhoSurface.OnResume();

            // if was resumed by the Camera Permission dialog
            LaunchUrho();
        }
        protected override void OnPause()
        {
            UrhoSurface.OnPause();
            base.OnPause();
        }

        protected override void OnDestroy()
        {
            UrhoSurface.OnDestroy();
            base.OnDestroy();
        }

        public override void OnBackPressed()
        {
            UrhoSurface.OnDestroy();
            Finish();
        }

        public override void OnLowMemory()
        {
            UrhoSurface.OnLowMemory();
            base.OnLowMemory();
        }

        private void ChangeVisibilityOfHUD(bool state)
        {
            ToggleButton poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            ToggleButton lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            SeekBar heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            SeekBar rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            Button prevControlsScreenButton = FindViewById<Button>(Resource.Id.prevControlsScreenButton);
            Button nextControlsScreenButton = FindViewById<Button>(Resource.Id.nextControlsScreenButton);
            Button addLampButton = FindViewById<Button>(Resource.Id.addLampButton);
            Button createInstalationButton = FindViewById<Button>(Resource.Id.createInstalationButton);
            Button deleteAllLampsButton = FindViewById<Button>(Resource.Id.deleteAllLampsButton);
            Button editLampsButton = FindViewById<Button>(Resource.Id.editLampsButton);
            Button prevSelLampButton = FindViewById<Button>(Resource.Id.prevSelLampButton);
            Button nextSelLampButton = FindViewById<Button>(Resource.Id.nextSelLampButton);

            if (state)
            {
                arrender.ShowingHUD();

                prevControlsScreenButton.Visibility = Android.Views.ViewStates.Visible;
                nextControlsScreenButton.Visibility = Android.Views.ViewStates.Visible;
                SetHud(hudScreenNum);
            }
            else
            {
                arrender.HidingHUD();

                poleButton.Visibility = Android.Views.ViewStates.Invisible;
                lightButton.Visibility = Android.Views.ViewStates.Invisible;
                heightSeekBar.Visibility = Android.Views.ViewStates.Invisible;
                rotateSeekBar.Visibility = Android.Views.ViewStates.Invisible;
                prevControlsScreenButton.Visibility = Android.Views.ViewStates.Invisible;
                nextControlsScreenButton.Visibility = Android.Views.ViewStates.Invisible;
                editLampsButton.Visibility = ViewStates.Invisible;
                createInstalationButton.Visibility = ViewStates.Invisible;
                deleteAllLampsButton.Visibility = ViewStates.Invisible;
                addLampButton.Visibility = ViewStates.Invisible;
                prevSelLampButton.Visibility = ViewStates.Invisible;
                nextSelLampButton.Visibility = ViewStates.Invisible;
            }
        }

        public void OnValueChange(NumberPicker picker, int oldVal, int newVal)
        {
            
        }

        public class NumOfNewLampsDialog : Android.App.DialogFragment, NumberPicker.IOnValueChangeListener
        {
            private readonly Context _context;
            private readonly int _min = 2, _max = 100, _current = 2;
            private readonly NumberPicker.IOnValueChangeListener _listener;
            public MainActivity mainActivity;

            public NumOfNewLampsDialog(MainActivity context, NumberPicker.IOnValueChangeListener listener, int current)
            {
                _context = context;
                _listener = listener;
                mainActivity = context;
                _current = current;
            }

            public override Dialog OnCreateDialog(Bundle savedState)
            {
                var inflater = (LayoutInflater)_context.GetSystemService(Context.LayoutInflaterService);
                var view = inflater.Inflate(Resource.Layout.NumOfNewLampsDialog, null);
                var numberPicker = view.FindViewById<NumberPicker>(Resource.Id.numberPicker);
                numberPicker.MaxValue = _max;
                numberPicker.MinValue = _min;
                numberPicker.Value = _current;
                numberPicker.SetOnValueChangedListener(_listener);

                var dialog = new Android.App.AlertDialog.Builder(_context);
                dialog.SetTitle("Select number of new lamps:");
                dialog.SetView(view);
                dialog.SetNegativeButton("Cancel", (s, a) => { mainActivity.arrender.StartCreatingNewInstallation(-1); });
                dialog.SetPositiveButton("OK", (s, a) => { mainActivity.arrender.StartCreatingNewInstallation(view.FindViewById<NumberPicker>(Resource.Id.numberPicker).Value); });
                return dialog.Create();
            }

            public void OnValueChange(NumberPicker picker, int oldVal, int newVal)
            {
                _listener.OnValueChange(picker, oldVal, newVal);
            }
        }
    }
}
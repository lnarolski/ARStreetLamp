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

namespace ARStreetLamp
{
    [Activity(Label = "ARStreetLamp", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        bool launched;
        ARRender arrender;
        RelativeLayout placeholder;
        UrhoSurfacePlaceholder surface;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            placeholder = FindViewById<RelativeLayout>(Resource.Id.UrhoSurfacePlaceHolder);

            LaunchUrho();
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

            launched = true;
            surface = UrhoSurface.CreateSurface(this);
            placeholder.AddView(surface);
            arrender = await surface.Show<ARRender>(
                new Urho.ApplicationOptions
                {
                    ResourcePaths = new[] { "LampsData" }
                });

            SetTitle(Resource.String.app_name);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
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
    }
}
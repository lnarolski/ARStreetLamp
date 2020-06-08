package crc64c8a5a5af67626084;


public class NumOfNewLampsDialog
	extends android.app.DialogFragment
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCreateDialog:(Landroid/os/Bundle;)Landroid/app/Dialog;:GetOnCreateDialog_Landroid_os_Bundle_Handler\n" +
			"";
		mono.android.Runtime.register ("ARStreetLamp.NumOfNewLampsDialog, ARStreetLamp", NumOfNewLampsDialog.class, __md_methods);
	}


	public NumOfNewLampsDialog ()
	{
		super ();
		if (getClass () == NumOfNewLampsDialog.class)
			mono.android.TypeManager.Activate ("ARStreetLamp.NumOfNewLampsDialog, ARStreetLamp", "", this, new java.lang.Object[] {  });
	}

	public NumOfNewLampsDialog (android.content.Context p0, int p1, android.widget.NumberPicker.OnValueChangeListener p2, int p3, int p4)
	{
		super ();
		if (getClass () == NumOfNewLampsDialog.class)
			mono.android.TypeManager.Activate ("ARStreetLamp.NumOfNewLampsDialog, ARStreetLamp", "Android.Content.Context, Mono.Android:System.Int32, mscorlib:Android.Widget.NumberPicker+IOnValueChangeListener, Mono.Android:System.Int32, mscorlib:System.Int32, mscorlib", this, new java.lang.Object[] { p0, p1, p2, p3, p4 });
	}


	public android.app.Dialog onCreateDialog (android.os.Bundle p0)
	{
		return n_onCreateDialog (p0);
	}

	private native android.app.Dialog n_onCreateDialog (android.os.Bundle p0);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}

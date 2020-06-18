package crc64c8a5a5af67626084;


public class MainActivity_NumOfNewLampsDialog
	extends android.app.DialogFragment
	implements
		mono.android.IGCUserPeer,
		android.widget.NumberPicker.OnValueChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCreateDialog:(Landroid/os/Bundle;)Landroid/app/Dialog;:GetOnCreateDialog_Landroid_os_Bundle_Handler\n" +
			"n_onValueChange:(Landroid/widget/NumberPicker;II)V:GetOnValueChange_Landroid_widget_NumberPicker_IIHandler:Android.Widget.NumberPicker/IOnValueChangeListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("ARStreetLamp.MainActivity+NumOfNewLampsDialog, ARStreetLamp", MainActivity_NumOfNewLampsDialog.class, __md_methods);
	}


	public MainActivity_NumOfNewLampsDialog ()
	{
		super ();
		if (getClass () == MainActivity_NumOfNewLampsDialog.class)
			mono.android.TypeManager.Activate ("ARStreetLamp.MainActivity+NumOfNewLampsDialog, ARStreetLamp", "", this, new java.lang.Object[] {  });
	}

	public MainActivity_NumOfNewLampsDialog (crc64c8a5a5af67626084.MainActivity p0, android.widget.NumberPicker.OnValueChangeListener p1, int p2)
	{
		super ();
		if (getClass () == MainActivity_NumOfNewLampsDialog.class)
			mono.android.TypeManager.Activate ("ARStreetLamp.MainActivity+NumOfNewLampsDialog, ARStreetLamp", "ARStreetLamp.MainActivity, ARStreetLamp:Android.Widget.NumberPicker+IOnValueChangeListener, Mono.Android:System.Int32, mscorlib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public android.app.Dialog onCreateDialog (android.os.Bundle p0)
	{
		return n_onCreateDialog (p0);
	}

	private native android.app.Dialog n_onCreateDialog (android.os.Bundle p0);


	public void onValueChange (android.widget.NumberPicker p0, int p1, int p2)
	{
		n_onValueChange (p0, p1, p2);
	}

	private native void n_onValueChange (android.widget.NumberPicker p0, int p1, int p2);

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

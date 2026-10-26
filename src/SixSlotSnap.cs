using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;

public class SixSlotSnap : ApplicationContext {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int Size; public RECT Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attr,out RECT rect,int size);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    delegate bool EnumProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
    delegate void WinEventProc(IntPtr hook,uint evt,IntPtr window,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    readonly HotkeyWindow receiver;
    readonly NotifyIcon tray;
    readonly string folder;
    readonly Selector selector;
    readonly IntPtr[] assigned=new IntPtr[6];
    readonly System.Windows.Forms.Timer resizeTimer=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer settleTimer=new System.Windows.Forms.Timer();
    readonly WinEventProc eventCallback;
    IntPtr eventHook,dragging;
    RECT lastDrag;
    bool changed;
    int settlePasses;
    public readonly Grid grid=new Grid();
    public class Grid {
        public RECT Work;
        public double Left=1.0/3,Right=2.0/3;
        public double[] Rows={0.5,0.5,0.5};
        public RECT RectFor(int slot) {
            int c=slot%3,r=slot/3,w=Work.R-Work.L,h=Work.B-Work.T;
            int[] x={Work.L,Work.L+(int)(w*Left),Work.L+(int)(w*Right),Work.R};
            int split=Work.T+(int)(h*Rows[c]);
            return new RECT{L=x[c],R=x[c+1],T=r==0?Work.T:split,B=r==0?split:Work.B};
        }
        static double Clamp(double value,double low,double high) { return Math.Max(low,Math.Min(high,value)); }
        public bool Adjust(int slot,RECT before,RECT after) {
            int c=slot%3,r=slot/3,w=Work.R-Work.L,h=Work.B-Work.T;
            if(w<=0 || h<=0) return false;
            double oldL=Left,oldR=Right,oldY=Rows[c];
            double minX=Math.Min(0.2,400.0/w),minY=Math.Min(0.3,200.0/h);
            if(Math.Abs((after.R-after.L)-(before.R-before.L))>1) {
                if(c>0 && Math.Abs(after.L-before.L)>1) {
                    if(c==1) Left=Clamp((after.L-Work.L)/(double)w,minX,Right-minX);
                    else Right=Clamp((after.L-Work.L)/(double)w,Left+minX,1-minX);
                }
                if(c<2 && Math.Abs(after.R-before.R)>1) {
                    if(c==0) Left=Clamp((after.R-Work.L)/(double)w,minX,Right-minX);
                    else Right=Clamp((after.R-Work.L)/(double)w,Left+minX,1-minX);
                }
            }
            if(Math.Abs((after.B-after.T)-(before.B-before.T))>1) {
                if(r==0 && Math.Abs(after.B-before.B)>1) Rows[c]=Clamp((after.B-Work.T)/(double)h,minY,1-minY);
                if(r==1 && Math.Abs(after.T-before.T)>1) Rows[c]=Clamp((after.T-Work.T)/(double)h,minY,1-minY);
            }
            return oldL!=Left || oldR!=Right || oldY!=Rows[c];
        }
    }
    static bool VisibleRect(IntPtr window,out RECT rect) {
        if(DwmGetWindowAttribute(window,9,out rect,Marshal.SizeOf(typeof(RECT)))==0) return true;
        return GetWindowRect(window,out rect);
    }
    static bool Near(RECT a,RECT b) { return Math.Abs(a.L-b.L)<16 && Math.Abs(a.T-b.T)<16 && Math.Abs(a.R-b.R)<16 && Math.Abs(a.B-b.B)<16; }
    void SetWork(RECT work) {
        if(work.L!=grid.Work.L || work.T!=grid.Work.T || work.R!=grid.Work.R || work.B!=grid.Work.B) {
            Array.Clear(assigned,0,6); grid.Work=work;
        }
    }
    void SaveLayout() {
        try {
            double[] values={grid.Left,grid.Right,grid.Rows[0],grid.Rows[1],grid.Rows[2]};
            File.WriteAllLines(Path.Combine(folder,"layout.txt"),Array.ConvertAll(values,delegate(double v) { return v.ToString("R",CultureInfo.InvariantCulture); }));
        } catch(Exception e) { Log("Could not save layout: "+e.Message); }
    }
    void LoadLayout() {
        string path=Path.Combine(folder,"layout.txt"); if(!File.Exists(path)) return;
        try {
            string[] lines=File.ReadAllLines(path); if(lines.Length!=5) return;
            double[] v=Array.ConvertAll(lines,delegate(string s) { return double.Parse(s,CultureInfo.InvariantCulture); });
            if(!(v[0]>=0.05 && v[1]-v[0]>=0.05 && v[1]<=0.95)) return;
            for(int i=2;i<5;i++) if(!(v[i]>=0.05 && v[i]<=0.95)) return;
            grid.Left=v[0]; grid.Right=v[1]; for(int i=0;i<3;i++) grid.Rows[i]=v[i+2];
        } catch(Exception e) { Log("Could not load layout: "+e.Message); }
    }
    void RecoverAssignments() {
        RECT[] recovered=new RECT[6];
        EnumWindows(delegate(IntPtr window,IntPtr ignored) {
            RECT rect;
            if(!IsWindowVisible(window) || IsIconic(window) || IsZoomed(window) || !VisibleRect(window,out rect)) return true;
            if(window==selector.Handle) return true;
            for(int i=0;i<6;i++) {
                RECT slot=grid.RectFor(i);
                bool columnMatch=Math.Abs(rect.L-slot.L)<16 && Math.Abs(rect.R-slot.R)<16;
                bool anchored=i<3?Math.Abs(rect.T-grid.Work.T)<16:Math.Abs(rect.B-grid.Work.B)<16;
                bool rowWindow=rect.B-rect.T<(grid.Work.B-grid.Work.T)*0.9 && rect.B-rect.T>100;
                if(assigned[i]==IntPtr.Zero && columnMatch && anchored && rowWindow) { assigned[i]=window; recovered[i]=rect; break; }
            }
            return true;
        },IntPtr.Zero);
        // Existing complementary pairs are better evidence than an old saved ratio.
        for(int c=0;c<3;c++) if(assigned[c]!=IntPtr.Zero && assigned[c+3]!=IntPtr.Zero && Math.Abs(recovered[c].B-recovered[c+3].T)<16) {
            grid.Rows[c]=((recovered[c].B+recovered[c+3].T)/2.0-grid.Work.T)/(grid.Work.B-grid.Work.T);
        }
        Log("Recovered "+Array.FindAll(assigned,delegate(IntPtr h) { return h!=IntPtr.Zero; }).Length+" occupied slots.");
    }
    void ApplyLayout() {
        for(int i=0;i<6;i++) {
            IntPtr w=assigned[i];
            if(!IsWindow(w)) { assigned[i]=IntPtr.Zero; continue; }
            // Do not fight the operating system's active sizing loop.
            if(w!=dragging && !IsIconic(w) && !IsZoomed(w)) PlaceWindow(w,grid.RectFor(i),false);
        }
    }
    void UpdateDrag() {
        if(dragging==IntPtr.Zero) return;
        int slot=Array.IndexOf(assigned,dragging); RECT current;
        if(slot<0 || !VisibleRect(dragging,out current) || IsZoomed(dragging)) return;
        if(grid.Adjust(slot,lastDrag,current)) { changed=true; ApplyLayout(); }
        VisibleRect(dragging,out lastDrag);
    }
    void OnWindowEvent(IntPtr hook,uint evt,IntPtr window,int objectId,int childId,uint thread,uint time) {
        try {
            if(evt==10 && Array.IndexOf(assigned,window)>=0) {
                settleTimer.Stop();
                dragging=window; changed=false; VisibleRect(window,out lastDrag); resizeTimer.Start();
                Log("Drag start slot "+(Array.IndexOf(assigned,window)+1));
            } else if(evt==11 && window==dragging) {
                UpdateDrag(); resizeTimer.Stop(); int slot=Array.IndexOf(assigned,window); RECT actual;
                // A changed divider remains managed even if Windows adjusts the source
                // at release. Only an actual move-out should detach a window.
                if(slot>=0 && (IsZoomed(window) || (!changed && VisibleRect(window,out actual) && !Near(actual,grid.RectFor(slot))))) assigned[slot]=IntPtr.Zero;
                dragging=IntPtr.Zero;
                if(changed) { ApplyLayout(); SaveLayout(); settlePasses=0; settleTimer.Start(); }
                Log("Drag end slot "+(slot+1)+"; resized="+changed);
            }
        } catch(Exception e) { Log("Resize: "+e.Message); }
    }
    class Selector : Form {
        public Action<int> Selected;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        int hovered=-1;
        public Selector() {
            FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
            AutoScaleMode=AutoScaleMode.None; DoubleBuffered=true;
            BackColor=Color.FromArgb(35,37,40); ForeColor=Color.White;
            Font=new Font("Segoe UI",10); Width=360; Height=104;
            AccessibleName="Six slot snap selector";
            timer.Interval=3000;
            timer.Tick+=delegate { if(!Bounds.Contains(Cursor.Position)) { Height=25; timer.Stop(); Invalidate(); } };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000080; return p; } }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        public void Place() {
            MONITORINFO m=new MONITORINFO(); m.Size=Marshal.SizeOf(m);
            if(GetMonitorInfo(MonitorFromWindow(GetForegroundWindow(),2),ref m)) Location=new Point(m.Work.L+(m.Work.R-m.Work.L-Width)/2,m.Work.T+3);
        }
        public void Reveal() { Place(); Height=104; Show(); ShowWindow(Handle,4); timer.Start(); Invalidate(); }
        int Hit(Point p) {
            if(Height<100 || p.X<9 || p.X>=351 || p.Y<28 || p.Y>=96) return -1;
            int c=(p.X-9)/114,r=(p.Y-28)/34;
            return r*3+c;
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Height=104; timer.Stop(); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered=-1; timer.Interval=1000; timer.Start(); Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h=Hit(e.Location); if(h!=hovered) { hovered=h; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); int hit=Hit(e.Location);
            if(e.Button==MouseButtons.Left && hit>=0 && Selected!=null) Selected(hit);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            TextRenderer.DrawText(e.Graphics,Height<100?"Hover to snap - 3 columns / 2 rows":"SNAP WINDOW     Ctrl+Alt+1-6",Font,new Rectangle(9,3,342,22),Color.FromArgb(215,219,225),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            if(Height<100) return;
            for(int i=0;i<6;i++) {
                Rectangle cell=new Rectangle(9+(i%3)*114,28+(i/3)*34,108,28);
                using(var brush=new SolidBrush(i==hovered?Color.FromArgb(50,112,175):Color.FromArgb(62,65,70))) e.Graphics.FillRectangle(brush,cell);
                using(var pen=new Pen(Color.FromArgb(110,116,125))) e.Graphics.DrawRectangle(pen,cell);
                TextRenderer.DrawText(e.Graphics,(i+1).ToString(),Font,cell,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            }
        }
        protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose(); Font.Dispose(); } base.Dispose(disposing); }
    }
    class HotkeyWindow : NativeWindow {
        public Action<int> Pressed;
        public HotkeyWindow() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x312 && Pressed!=null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }
    void Log(string text) { File.AppendAllText(Path.Combine(folder,"status.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine); }
    public static RECT Slot(RECT work,int slot) {
        int c=slot%3, r=slot/3, w=work.R-work.L,h=work.B-work.T;
        return new RECT { L=work.L+w*c/3, R=work.L+w*(c+1)/3,T=work.T+h*r/2,B=work.T+h*(r+1)/2 };
    }
    public static string Geometry() {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        MONITORINFO m=new MONITORINFO(); m.Size=Marshal.SizeOf(m);
        if(!GetMonitorInfo(MonitorFromWindow(GetForegroundWindow(),2),ref m)) throw new Exception("Cannot read monitor.");
        string result="Monitor: "+(m.Monitor.R-m.Monitor.L)+" x "+(m.Monitor.B-m.Monitor.T)+"; work area: "+(m.Work.R-m.Work.L)+" x "+(m.Work.B-m.Work.T);
        for(int i=0;i<6;i++) { RECT s=Slot(m.Work,i); result+=Environment.NewLine+"Slot "+(i+1)+": "+s.L+","+s.T+" "+(s.R-s.L)+" x "+(s.B-s.T); }
        return result;
    }
    void Snap(int slot) {
        IntPtr w=GetForegroundWindow(); if(w==IntPtr.Zero) return;
        MONITORINFO m=new MONITORINFO(); m.Size=Marshal.SizeOf(m);
        if(!GetMonitorInfo(MonitorFromWindow(w,2),ref m)) return;
        SetWork(m.Work);
        if(IsZoomed(w)) ShowWindow(w,9);
        if(PlaceWindow(w,grid.RectFor(slot),true)) {
            for(int i=0;i<6;i++) if(assigned[i]==w) assigned[i]=IntPtr.Zero;
            assigned[slot]=w;
        }
    }
    bool PlaceWindow(IntPtr w,RECT s,bool notify) {
        RECT outer,visible;
        if(!GetWindowRect(w,out outer)) return false;
        if(DwmGetWindowAttribute(w,9,out visible,Marshal.SizeOf(typeof(RECT)))!=0) visible=outer;
        int left=visible.L-outer.L,top=visible.T-outer.T,right=outer.R-visible.R,bottom=outer.B-visible.B;
        if(!SetWindowPos(w,IntPtr.Zero,s.L-left,s.T-top,s.R-s.L+left+right,s.B-s.T+top+bottom,0x14)) {
            Log("Placement failed: Win32 "+Marshal.GetLastWin32Error());
            if(notify) tray.ShowBalloonTip(3500,"Six Slot Snap","This window could not be moved. Elevated apps may require running the controller as administrator.",ToolTipIcon.Warning);
            return false;
        }
        return true;
    }
    SixSlotSnap(string path) {
        folder=path; SetThreadDpiAwarenessContext(new IntPtr(-4));
        MONITORINFO monitor=new MONITORINFO(); monitor.Size=Marshal.SizeOf(monitor);
        if(GetMonitorInfo(MonitorFromWindow(GetForegroundWindow(),2),ref monitor)) SetWork(monitor.Work);
        LoadLayout();
        receiver=new HotkeyWindow();
        for(int i=1;i<=6;i++) {
            if(!RegisterHotKey(receiver.Handle,i,0x4003,(uint)(0x30+i))) {
                for(int j=1;j<i;j++) UnregisterHotKey(receiver.Handle,j);
                receiver.DestroyHandle();
                throw new Exception("Ctrl+Alt+"+i+" is already in use. No shortcuts were installed.");
            }
        }
        ContextMenuStrip menu=new ContextMenuStrip();
        menu.Items.Add("Ctrl+Alt+1 / 2 / 3: top row");
        menu.Items.Add("Ctrl+Alt+4 / 5 / 6: bottom row");
        menu.Items.Add("Show / hide top selector",null,delegate { if(selector.Visible) selector.Hide(); else selector.Reveal(); });
        menu.Items.Add("Reset to equal columns and rows",null,delegate {
            grid.Left=1.0/3; grid.Right=2.0/3; for(int i=0;i<3;i++) grid.Rows[i]=0.5;
            ApplyLayout(); SaveLayout();
        });
        menu.Items.Add("Exit",null,delegate { ExitThread(); });
        tray=new NotifyIcon { Icon=System.Drawing.SystemIcons.Application,Text="Six Slot Snap - Ctrl+Alt+1 through 6",ContextMenuStrip=menu,Visible=true };
        receiver.Pressed=delegate(int id) { try { Snap(id-1); } catch(Exception e) { Log(e.ToString()); } };
        selector=new Selector();
        selector.Selected=delegate(int slot) { try { Snap(slot); } catch(Exception e) { Log(e.ToString()); } };
        selector.Reveal();
        RecoverAssignments();
        SaveLayout();
        settleTimer.Interval=100;
        settleTimer.Tick+=delegate {
            try { if(dragging==IntPtr.Zero) ApplyLayout(); } catch(Exception e) { Log("Settle: "+e.Message); }
            if(++settlePasses>=3) settleTimer.Stop();
        };
        resizeTimer.Interval=30;
        resizeTimer.Tick+=delegate { try { UpdateDrag(); } catch(Exception e) { Log("Resize tick: "+e.Message); } };
        eventCallback=OnWindowEvent;
        eventHook=SetWinEventHook(10,11,IntPtr.Zero,eventCallback,0,0,0);
        if(eventHook==IntPtr.Zero) Log("ERROR: Windows resize event hook could not be installed.");
        Log("Ready. "+Geometry());
    }
    protected override void ExitThreadCore() {
        resizeTimer.Stop(); resizeTimer.Dispose();
        settleTimer.Stop(); settleTimer.Dispose();
        if(eventHook!=IntPtr.Zero) UnhookWinEvent(eventHook);
        for(int i=1;i<=6;i++) UnregisterHotKey(receiver.Handle,i);
        receiver.DestroyHandle(); selector.Dispose(); tray.Visible=false; tray.Dispose(); Log("Stopped."); base.ExitThreadCore();
    }
    [STAThread] public static void Main(string[] args) {
        string folder=AppDomain.CurrentDomain.BaseDirectory;
        if(args.Length>0 && args[0]=="--preview") {
            SetThreadDpiAwarenessContext(new IntPtr(-4));
            using(var panel=new Selector()) using(var bitmap=new Bitmap(panel.Width,panel.Height)) {
                panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(folder,"selector-preview.png"));
            }
            return;
        }
        if(args.Length>0 && args[0]=="--check") { File.WriteAllText(Path.Combine(folder,"geometry.txt"),Geometry()); return; }
        bool created;
        using(Mutex mutex=new Mutex(true,"Local\\SixSlotSnapController",out created)) {
            if(!created) return;
            try { Application.EnableVisualStyles(); Application.Run(new SixSlotSnap(folder)); }
            catch(Exception e) { File.AppendAllText(Path.Combine(folder,"status.log"),e.ToString()+Environment.NewLine); MessageBox.Show(e.Message,"Six Slot Snap"); }
        }
    }
}

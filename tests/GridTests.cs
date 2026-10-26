using System;

class GridTests {
    static int checks;
    static void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static SixSlotSnap.Grid NewGrid(int x,int y) {
        var g=new SixSlotSnap.Grid();
        g.Work=new SixSlotSnap.RECT{L=x,T=y,R=x+5120,B=y+2160};
        return g;
    }
    static void Coverage(SixSlotSnap.Grid g) {
        for(int c=0;c<3;c++) {
            var top=g.RectFor(c); var bottom=g.RectFor(c+3);
            Check(top.L==bottom.L && top.R==bottom.R,"Column edges separated");
            Check(top.B==bottom.T,"Rows have a gap or overlap");
            Check(top.T==g.Work.T && bottom.B==g.Work.B,"Outer row edge moved");
            Check(top.L<top.R && top.T<top.B && bottom.T<bottom.B,"Non-positive slot size");
            if(c<2) Check(top.R==g.RectFor(c+1).L,"Columns have a gap or overlap");
        }
        Check(g.RectFor(0).L==g.Work.L && g.RectFor(2).R==g.Work.R,"Outer column edge moved");
    }
    static void Run(int x,int y) {
        var g=NewGrid(x,y); Coverage(g);
        // Every shared vertical boundary, dragged from either row and side.
        for(int slot=0;slot<6;slot++) for(int edge=0;edge<2;edge++) {
            int c=slot%3;
            if((edge==0 && c==0) || (edge==1 && c==2)) continue;
            double[] rows=(double[])g.Rows.Clone();
            var before=g.RectFor(slot); var after=before;
            if(edge==0) after.L+=70; else after.R-=55;
            Check(g.Adjust(slot,before,after),"Shared column edge ignored");
            for(int i=0;i<3;i++) Check(g.Rows[i]==rows[i],"Width resize changed a row height");
            Coverage(g);
        }
        // Each horizontal divider affects only its own top/bottom pair.
        for(int slot=0;slot<6;slot++) {
            int c=slot%3; double left=g.Left,right=g.Right;
            double[] rows=(double[])g.Rows.Clone();
            var before=g.RectFor(slot); var after=before;
            if(slot<3) after.B+=90; else after.T-=50;
            Check(g.Adjust(slot,before,after),"Shared row edge ignored");
            Check(left==g.Left && right==g.Right,"Height resize changed column widths");
            for(int i=0;i<3;i++) if(i!=c) Check(g.Rows[i]==rows[i],"Unrelated row divider moved");
            Coverage(g);
        }
        var start=g.RectFor(1); var moved=start;
        moved.L+=80; moved.R+=80; moved.T+=35; moved.B+=35;
        Check(!g.Adjust(1,start,moved),"A move was treated as a resize");
        start=g.RectFor(1); var corner=start; corner.L+=45; corner.B+=60;
        double oldLeft=g.Left,oldRow=g.Rows[1];
        Check(g.Adjust(1,start,corner) && g.Left!=oldLeft && g.Rows[1]!=oldRow,"Corner did not update both axes");
        Coverage(g);
        var random=new Random(42);
        for(int i=0;i<500;i++) {
            int slot=random.Next(6),c=slot%3;
            var before=g.RectFor(slot); var after=before;
            if(c>0) after.L+=random.Next(-4000,4000);
            else after.R+=random.Next(-4000,4000);
            if(slot<3) after.B+=random.Next(-3000,3000);
            else after.T+=random.Next(-3000,3000);
            g.Adjust(slot,before,after); Coverage(g);
        }
    }
    static int Main() {
        try { Run(0,0); Run(-5120,40); Console.WriteLine("PASS: "+checks+" geometry checks; no desktop windows moved."); return 0; }
        catch(Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}

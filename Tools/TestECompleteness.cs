// Standalone regression harness: compile against Assembly-CSharp and UnityEngine.CoreModule.
using System;
using System.Collections.Generic;
using MagicTouch.Recognition;

public static class TestECompleteness
{
    private static void Line(List<Point> p, float x1, float y1, float x2, float y2, int id, int samples = 20)
    {
        for (int i=0;i<=samples;i++)
        {
            float t=(float)i/samples;
            p.Add(new Point(x1+(x2-x1)*t,y1+(y2-y1)*t,id));
        }
    }

    private static List<Point> Make(bool bottom=true, bool middle=true, bool top=true, bool spine=true, int samples=20)
    {
        var p=new List<Point>();
        if (spine) Line(p,0,0,0,100,0,samples);
        if (bottom) Line(p,0,0,80,0,1,samples);
        if (middle) Line(p,0,50,50,50,2,samples);
        if (top) Line(p,0,100,80,100,3,samples);
        return p;
    }

    private static void Check(string name, List<Point> points, bool expected)
    {
        bool actual=GestureCompletionPolicy.HasRequiredStructure(points,GestureSymbol.E);
        if (actual!=expected) throw new Exception(name+": expected "+expected+", got "+actual);
        Console.WriteLine("PASS "+name);
    }

    public static void Main()
    {
        Check("complete multistroke E",Make(),true);
        Check("missing middle",Make(middle:false),false);
        Check("missing top",Make(top:false),false);
        Check("missing bottom",Make(bottom:false),false);
        Check("missing spine",Make(spine:false),false);
        Check("sparse sampling",Make(samples:1),true);
        var tick=Make(middle:false);
        Line(tick,60,50,65,50,4);
        Check("tiny isolated middle tick",tick,false);
        var continuous=new List<Point>();
        Line(continuous,80,100,0,100,0);
        Line(continuous,0,100,0,0,0);
        Line(continuous,0,0,80,0,0);
        Line(continuous,80,0,0,0,0);
        Line(continuous,0,0,0,50,0);
        Line(continuous,0,50,50,50,0);
        Check("single stroke with retracing",continuous,true);
        var transformed=Make();
        for (int i=0;i<transformed.Count;i++)
        {
            Point p=transformed[i];
            transformed[i]=new Point(120+p.X*2,300+p.Y*2,p.StrokeID);
        }
        Check("translation and scale",transformed,true);
        var disconnected=new List<Point> {new Point(0,0,0),new Point(0,100,1),
            new Point(80,100,2),new Point(0,50,3),new Point(80,50,4),new Point(80,0,5)};
        Check("pen lifts cannot create segments",disconnected,false);
        if (!GestureCompletionPolicy.HasRequiredStructure(Make(middle:false),GestureSymbol.Caret))
            throw new Exception("Other symbols must remain unchanged.");
        Console.WriteLine("PASS other symbols unchanged");
    }
}

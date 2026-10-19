using System;using LestingCraft.Gameplay;
// Step 1.2 of stage 1 (space): flight physics core. Pure C#, no Unity. Run by core.yml.
class Check
{
 static int tests;static void Assert(bool value,string name){tests++;if(!value)throw new Exception(name);}
 static void Within(double value,double low,double high,string name){Assert(value>=low&&value<=high,name+" = "+value.ToString("R")+" not in ["+low+", "+high+"]");}
 static void Main()
 {
  const double R=OrbitalFlight.REarth, GM=OrbitalFlight.GMEarth;
  double r200=R+200e3;

  // Closed-form physics (game constants are real values).
  Within(OrbitalFlight.CircularSpeed(GM,r200),7780,7795,"LEO 200 km circular speed (m/s)");
  Within(OrbitalFlight.Tsiolkovsky(311,Math.E,1),3045,3055,"Tsiolkovsky vacuum Isp 311 s, mass ratio e (m/s)");
  Within(OrbitalFlight.Tsiolkovsky(282,2,1),1910,1925,"Tsiolkovsky sea-level Isp 282 s, mass ratio 2 (m/s)"); // 282*g0*ln2 = 1917
  Within(OrbitalFlight.HohmannDeparture(r200,OrbitalFlight.MoonDistance),3100,3160,"Hohmann LEO->Moon distance departure dv (m/s)");
  Within(OrbitalFlight.HohmannTime(r200,OrbitalFlight.MoonDistance)/86400.0,4.9,5.1,"Hohmann LEO->Moon distance time (days)");
  Within(OrbitalFlight.Density(8500)/OrbitalFlight.Rho0,0.3678,0.3680,"density at one scale height is rho0/e");
  Within(OrbitalFlight.Density(100e3),0,1e-5,"density at Karman line is negligible");
  Within(OrbitalFlight.CircularSpeed(OrbitalFlight.GMMoon,OrbitalFlight.RMoon),1675,1685,"lunar surface circular speed (m/s)");
  Within(OrbitalFlight.GMMoon/(OrbitalFlight.RMoon*OrbitalFlight.RMoon),1.61,1.63,"lunar surface gravity g = GM/R^2 (m/s^2)");

  // RK4 order: halving the step should cut the one-period position error by about 16 (order 4).
  double a=r200,T=2*Math.PI*Math.Sqrt(a*a*a/GM);
  var start=new OrbitalFlight.State(a,0,0,OrbitalFlight.CircularSpeed(GM,a),1000);
  double E0=OrbitalFlight.Energy(start);
  Func<double,double> errorAt=dt=>{var end=OrbitalFlight.Propagate(start,T,dt,OrbitalFlight.Burn.Off,0);return Math.Sqrt(Math.Pow(end.X-start.X,2)+Math.Pow(end.Y-start.Y,2));};
  double e1=errorAt(120),e2=errorAt(60);
  Assert(e2>0&&e1/e2>10&&e1/e2<25,"RK4 order near 4: error ratio "+(e1/e2).ToString("F2"));
  Assert(errorAt(30)<1000,"one orbit closes within 1 km at 30 s steps");

  // Energy conservation over 10 orbits (no drag, no thrust).
  var tenOrbits=OrbitalFlight.Propagate(start,10*T,10,OrbitalFlight.Burn.Off,0);
  double drift=Math.Abs(OrbitalFlight.Energy(tenOrbits)-E0)/Math.Abs(E0);
  Assert(drift<1e-6,"energy drift over 10 orbits "+drift.ToString("E2"));
  Assert(tenOrbits.M==start.M,"coasting keeps mass");

  // Drag: a 10 m^2 vehicle at 200 km loses energy; thrust-free drag never gains it.
  var dragged=OrbitalFlight.Propagate(start,T,10,OrbitalFlight.Burn.Off,10);
  Assert(OrbitalFlight.Energy(dragged)<E0,"drag removes orbital energy");

  // Thrust mass flow: m(t) = m0 - F*t/(Isp*g0) exactly.
  var burn=new OrbitalFlight.Burn(500000,311);
  var burned=OrbitalFlight.Propagate(new OrbitalFlight.State(a,0,0,7790,100000),100,1,burn,0);
  double expectedMass=100000-500000*100/(311*OrbitalFlight.G0);
  Assert(Math.Abs(burned.M-expectedMass)<1e-6*expectedMass,"mass flow matches F/(Isp g0)");

  // Determinism: the same inputs give identical bits (needed for server/client agreement).
  var one=OrbitalFlight.Propagate(start,5000,10,OrbitalFlight.Burn.Off,0);var two=OrbitalFlight.Propagate(start,5000,10,OrbitalFlight.Burn.Off,0);
  Assert(BitConverter.DoubleToInt64Bits(one.X)==BitConverter.DoubleToInt64Bits(two.X)&&BitConverter.DoubleToInt64Bits(one.Vy)==BitConverter.DoubleToInt64Bits(two.Vy),"bitwise deterministic");

  // Trans-lunar injection as a finite burn from LEO. Guidance cuts thrust when the specific energy reaches that of the
  // Hohmann ellipse with apogee at lunar distance (a finite burn gains more than the impulsive dv because of gravity loss,
  // so a speed-gain cut-off would overshoot). Then the apogee from the integrated state must match lunar distance.
  double rApo=OrbitalFlight.MoonDistance,aHoh=(r200+rApo)/2,Etarget=-GM/(2*aHoh);
  var s=new OrbitalFlight.State(r200,0,0,OrbitalFlight.CircularSpeed(GM,r200),100000);
  double v0=Math.Sqrt(s.Vx*s.Vx+s.Vy*s.Vy),dvTarget=OrbitalFlight.HohmannDeparture(r200,rApo);
  var tli=new OrbitalFlight.Burn(500000,311);
  double burnTime=0;int steps=0;
  while(OrbitalFlight.Energy(s)<Etarget&&steps<4000)
  {
   // Shorten the last step so the cut-off lands on the target energy (dE/dt = a*v along the burn).
   double vNow=Math.Sqrt(s.Vx*s.Vx+s.Vy*s.Vy),rate=(tli.Thrust/s.M)*vNow;
   double dt=Math.Min(0.5,(Etarget-OrbitalFlight.Energy(s))/rate);
   s=OrbitalFlight.Rk4(s,dt,tli,0);burnTime+=dt;steps++;
  }
  Assert(steps<4000,"TLI burn cut off by energy within 2000 s");
  double achieved=Math.Sqrt(s.Vx*s.Vx+s.Vy*s.Vy)-v0;
  Within(achieved/dvTarget,0.90,1.0,"speed gain vs impulsive Hohmann dv (gravity loss 0-10 %)");
  double propellant=100000-s.M,ideal=100000*(1-Math.Exp(-dvTarget/(311*OrbitalFlight.G0)));
  Within(propellant/ideal,0.98,1.10,"propellant vs ideal rocket equation (gravity and finite-burn losses)");
  double energyAfter=OrbitalFlight.Energy(s);
  Assert(energyAfter<0,"transfer is a bound ellipse");
  double sma=-GM/(2*energyAfter),h=s.X*s.Vy-s.Y*s.Vx;
  double ecc=Math.Sqrt(Math.Max(0,1+2*energyAfter*h*h/(GM*GM)));
  double apogee=sma*(1+ecc),perigee=sma*(1-ecc);
  Within(apogee/rApo,0.99,1.01,"transfer apogee vs Moon distance (ratio)");
  Within(perigee/1e6,6.0,7.0,"transfer perigee stays near LEO (1e3 km)");
  Within(burnTime,300,1200,"TLI burn time (s)");
  double flight=OrbitalFlight.HohmannTime(r200,rApo);
  Console.WriteLine("INFO: Hohmann LEO->Moon "+(flight/86400).ToString("F2")+" days; game 30 min needs x"+(flight/1800).ToString("F0")+"; x144 gives "+(flight/144/60).ToString("F0")+" min. TLI burn "+burnTime.ToString("F0")+" s, speed gain "+achieved.ToString("F0")+" m/s, apogee "+(apogee/1e6).ToString("F0")+" thousand km, perigee "+(perigee/1e3).ToString("F0")+" km.");

  Console.WriteLine("PASS: "+tests+" assertions; RK4 order, energy drift, drag, mass flow, determinism, finite-burn TLI and lunar apogee. Pure C# core only.");
 }
}

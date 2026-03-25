using System;
using Sandbox.NPCs;
using Sandbox.NPCs.Wanderers;

namespace Sandbox.Weapons;

public sealed class MiniTurret : Component
{
	[Property] public GameObject YawPivot { get; set; }
	[Property] public GameObject Cannon { get; set; }
	[Property] public GameObject Muzzle { get; set; }
	[Property] public string TargetTag { get; set; } = "npc";
	[Property] public float Range { get; set; } = 1600f;
	[Property] public float YawTurnSpeed { get; set; } = 6f;
	[Property] public float CannonTurnSpeed { get; set; } = 10f;
	[Property] public float FireRate { get; set; } = 5f;
	[Property] public int Damage { get; set; } = 10;
	[Property] public float TargetAimHeight { get; set; } = 56f;
	[Property] public string BeamParticle { get; set; } = "particles/tracer/trail_smoke.vpcf";
	[Property] public GameObject BloodImpactPrefab { get; set; }
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public float IdleSweepAngle { get; set; } = 35f;
	[Property] public float IdleSweepSpeed { get; set; } = 1.2f;
	[Property] public Color SearchBeamColor { get; set; } = Color.Yellow;

	private GameObject _target;
	private TimeSince _timeSinceLastShot;
	private TimeSince _timeSinceSearch;
	private Rotation _baseYawLocalRotation;
	private Rotation _baseCannonLocalRotation;

	protected override void OnStart()
	{
		_timeSinceSearch = 999f;
		_baseYawLocalRotation = YawPivot?.LocalRotation ?? Rotation.Identity;
		_baseCannonLocalRotation = Cannon?.LocalRotation ?? Rotation.Identity;
	}

	protected override void OnUpdate()
	{
		if ( YawPivot == null || Cannon == null )
			return;

		// Refresh target ownership from the search beam a few times per second.
		if ( _timeSinceSearch > 0.2f )
		{
			_target = FindTargetFromSearchTrace();
			_timeSinceSearch = 0f;
		}

		if ( _target == null || !_target.IsValid() )
		{
			UpdateIdleSweep();
			DrawSearchBeam();
			return;
		}

		var aimPoint = GetAimPoint( _target );
		UpdateAim( aimPoint );

		if ( _timeSinceLastShot < (1f / FireRate) )
			return;

		if ( !CanSeeTarget( aimPoint ) )
			return;

		FireAt( aimPoint );
		_timeSinceLastShot = 0f;
	}

	private GameObject FindTargetFromSearchTrace()
	{
		var start = Muzzle?.WorldPosition ?? Cannon?.WorldPosition ?? WorldPosition;
		var direction = (Muzzle?.WorldRotation ?? Cannon?.WorldRotation ?? WorldRotation).Forward;
		var end = start + direction * Range;

		// A trace is a raycast through the scene. Here we cast straight forward from the
		// turret's muzzle/cannon to see if the idle search beam is touching an NPC.
		var trace = Scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		return ResolveTarget( trace.GameObject );
	}

	private Vector3 GetAimPoint( GameObject target )
	{
		return target.WorldPosition + Vector3.Up * TargetAimHeight;
	}

	private void UpdateAim( Vector3 aimPoint )
	{
		// Yaw pivot rotates flat on the base, cannon handles the final pitch.
		var yawOrigin = YawPivot.WorldPosition;

		// Flatten the target direction onto the yaw pivot's height so the base only solves
		// left/right turning and doesn't tilt up or down.
		var flatDirection = (aimPoint.WithZ( yawOrigin.z ) - yawOrigin).Normal;
		if ( !flatDirection.IsNearZeroLength )
		{
			var targetYaw = Rotation.LookAt( flatDirection );
			YawPivot.WorldRotation = Rotation.Slerp( YawPivot.WorldRotation, targetYaw, Time.Delta * YawTurnSpeed );
		}

		// The cannon uses the full 3D direction, so this step adds the up/down aim needed
		// to actually point the barrel at the target.
		var cannonDirection = (aimPoint - Cannon.WorldPosition).Normal;
		if ( !cannonDirection.IsNearZeroLength )
		{
			var targetRotation = Rotation.LookAt( cannonDirection );
			Cannon.WorldRotation = Rotation.Slerp( Cannon.WorldRotation, targetRotation, Time.Delta * CannonTurnSpeed );
		}
	}

	private void UpdateIdleSweep()
	{
		if ( YawPivot == null || Cannon == null )
			return;

		// Sweep around the original local setup so idle never drags parts out of place.
		// Sin creates a smooth back-and-forth motion between -IdleSweepAngle and +IdleSweepAngle.
		var yawOffset = MathF.Sin( Time.Now * IdleSweepSpeed ) * IdleSweepAngle;
		var targetYaw = _baseYawLocalRotation * Rotation.FromYaw( yawOffset );
		YawPivot.LocalRotation = Rotation.Slerp( YawPivot.LocalRotation, targetYaw, Time.Delta * YawTurnSpeed );

		// Reset the cannon back toward its authored local rotation while idle so only the
		// yaw block is scanning.
		Cannon.LocalRotation = Rotation.Slerp( Cannon.LocalRotation, _baseCannonLocalRotation, Time.Delta * CannonTurnSpeed );
	}

	private void DrawSearchBeam()
	{
		var start = Muzzle?.WorldPosition ?? Cannon.WorldPosition;
		var direction = (Muzzle?.WorldRotation ?? Cannon.WorldRotation).Forward;
		var end = start + direction * Range;

		// Trace the beam so the debug line stops where it actually hits something instead of
		// always drawing all the way to max range.
		var trace = Scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		DebugOverlay.Line( start, trace.Hit ? trace.EndPosition : end, SearchBeamColor, 0.01f );
	}

	private bool CanSeeTarget( Vector3 aimPoint )
	{
		var start = Muzzle?.WorldPosition ?? Cannon.WorldPosition;

		// This line-of-sight check makes sure the turret only fires when nothing blocks the
		// path between the muzzle and the target's aim point.
		var trace = Scene.Trace.Ray( start, aimPoint )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit )
			return false;

		return trace.GameObject.Tags.Has( TargetTag ) || trace.GameObject.Root?.Tags.Has( TargetTag ) == true;
	}

	private void FireAt( Vector3 aimPoint )
	{
		var start = Muzzle?.WorldPosition ?? Cannon.WorldPosition;

		// Fire another trace down the barrel to find the real impact point for damage.
		var trace = Scene.Trace.Ray( start, aimPoint )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var npc = ResolveNpc( trace.GameObject );

		// For visuals we let the beam reach the aim point when we know we're on an NPC,
		// because collider hits often land slightly in front of the body and look short.
		var beamEnd = npc != null ? aimPoint : trace.EndPosition;

		DebugOverlay.Line( start, beamEnd, Color.Cyan, 0.08f );
		SpawnBeam( start, beamEnd, start.Distance( beamEnd ) );
		PlayFireSound( start );

		if ( !trace.Hit )
			return;

		if ( npc != null )
		{
			SpawnImpactParticles( trace.EndPosition, trace.Normal );
			npc.TakeDamage( Damage, trace.EndPosition, (trace.EndPosition - start).Normal * 150f );
		}
	}

	private GameObject ResolveTarget( GameObject hitObject )
	{
		var npc = ResolveNpc( hitObject );
		return npc?.GameObject;
	}

	private WanderersNpc ResolveNpc( GameObject hitObject )
	{
		if ( hitObject == null || !hitObject.IsValid() )
			return null;

		// Traces often hit a collider or child object, so walk upward through the hierarchy
		// to find the actual NPC gameplay component that owns the hit part.
		var npc = hitObject.Components.GetInAncestorsOrSelf<WanderersNpc>()
			?? hitObject.Root?.Components.Get<WanderersNpc>( FindMode.EverythingInSelf );

		if ( npc == null )
			return null;

		return npc.GameObject.Tags.Has( TargetTag ) ? npc : null;
	}

	private void SpawnBeam( Vector3 start, Vector3 end, float distance )
	{
		if ( string.IsNullOrWhiteSpace( BeamParticle ) )
			return;

		// The tracer particle uses control points:
		// 0 = beam start, 1 = beam end, 2 = distance data for the effect.
		var beam = new SceneParticles( Scene.SceneWorld, BeamParticle );
		beam.SetControlPoint( 0, start );
		beam.SetControlPoint( 1, end );
		beam.SetControlPoint( 2, distance );
		beam.PlayUntilFinished( Task );
	}

	private void SpawnImpactParticles( Vector3 position, Vector3 normal )
	{
		if ( BloodImpactPrefab == null )
			return;

		var impact = BloodImpactPrefab.Clone();
		impact.WorldPosition = position;
		impact.WorldRotation = Rotation.LookAt( normal );
		impact.Flags |= GameObjectFlags.NotNetworked;
		impact.DestroyAsync( 2f );
	}

	private void PlayFireSound( Vector3 position )
	{
		if ( FireSound is null )
			return;

		Sound.Play( FireSound, position );
	}
}

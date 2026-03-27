using System;
using Sandbox.Citizen;
using Sandbox.NPCs;
using Sandbox.NPCs.Wanderers;

namespace Sandbox.Player;

public sealed class PlayerActions : Component
{
	[Property] public PlayerController Player { get; set; }
	[Property] public GameObject PlayerCamera { get; set; }
	[Property] public SkinnedModelRenderer WeaponViewModel { get; set; }
	[Property, Group( "Weapon" )] public int MagazineSize { get; set; } = 30;
	[Property, Group( "Weapon" )] public int AmmoInMagazine { get; set; } = 30;
	[Property, Group( "Weapon" )] public int ReserveAmmo { get; set; } = 90;
	[Property, Group( "Weapon" )] public float FireRate { get; set; } = 0.1f;
	[Property, Group( "Weapon" )] public float ReloadDuration { get; set; } = 2.2f;
	[Property, Group( "Weapon" )] public int WeaponDamage { get; set; } = 25;
	[Property, Group( "Weapon" )] public float WeaponRange { get; set; } = 5000f;
	[Property, Group( "Weapon|Recoil" )] public float CameraRecoilPitch { get; set; } = 10.9f;
	[Property, Group( "Weapon|Recoil" )] public float CameraRecoilYaw { get; set; } = 10.55f;
	[Property, Group( "Weapon|Recoil" )] public float CameraRecoilRecoverSpeed { get; set; } = 201f;
	[Property, Group( "Weapon|Recoil" )] public float CameraRecoilSnappiness { get; set; } = 341f;
	[Property, Group( "Weapon|Effects" )] public GameObject MuzzleFlashOrigin { get; set; }
	[Property, Group( "Weapon|Effects" )] public GameObject MuzzleFlashPrefab { get; set; }
	[Property, Group( "Weapon|Sound" )] public SoundEvent FireSound { get; set; }
	[Property, Group( "Weapon|Sound" )] public SoundEvent DryFireSound { get; set; }
	[Property, Group( "Weapon|Sound" )] public SoundEvent ReloadSound { get; set; }
	[Property] public float Health { get; set; } = 100f;
	[Property] public float MaxHealth { get; set; } = 100f;
	[Property] public long Logs { get; set; }
	[Property] public long Rocks { get; set; }
	[Property] public GameObject Head { get; set; }
	[Property] public List<string> Inventory { get; set; } = new() { "Fist" };

	public int WoodcuttingLevel { get; set; } = 1;
	public long WoodcuttingExperience { get; set; } = 1;
	public int MiningLevel { get; set; } = 1;
	public long MiningExperience { get; set; }
	public int ActiveSlot = 0;
	public int Slots => 9;
	public float PunchCooldown = 2f;
	public ClothingContainer ClothingContainer;
	public bool IsCutting;
	public bool IsRocking;
	public bool IsFishing;
	private CitizenAnimationHelper _citizenAnimationHelper;
	public float CuttingSpeed = 1f;
	public float MiningSpeed = 1f;
	public long CuttingAmount = 1;
	public long MiningAmount = 1;
	public float Timer;
	private float _soundTimer;
	private float _saveTimer;
	private TimeSince _lastPunch;
	private TimeSince _timeSinceLastShot;
	private TimeSince _timeSinceDryFire;
	private readonly SoundEvent _resourceGained = new( "sounds/kenney/ui/drop_002.vsnd_c" ) { UI = true, Volume = 2 };
	private SceneTraceResult SceneTraceResult;
	private bool _isReloading;
	private float _reloadTimer;
	private Angles _cameraRecoilCurrent;
	private Angles _cameraRecoilTarget;

	protected override void OnStart()
	{
		InitializeComponents();
		ApplyClothing();
		LoadPlayerData();
		InitializeWeaponViewModel();
	}

	protected override void OnUpdate()
	{
		HandleInput();
		UpdateWeaponViewModel();
		UpdateCameraRecoil();
		StartAction( IsRocking, MiningSpeed, MiningAmount, GenericColliderController.ActionType.Rocking );
		StartAction( IsCutting, CuttingSpeed, CuttingAmount, GenericColliderController.ActionType.Cutting );
		SavePlayerData();
	}

	protected override void OnPreRender()
	{
		if ( PlayerCamera == null )
			return;

		PlayerCamera.LocalRotation *= Rotation.From( _cameraRecoilCurrent );
		
		
	}

	private void SavePlayerData()
	{
		_saveTimer += Time.Delta;
		if ( _saveTimer < 10f ) return;

		var playerData = new PlayerData
		{
			Wood = Logs,
			Rocks = Rocks,
			MiningLevel = MiningLevel,
			MiningExperience = MiningExperience,
			WoodcuttingLevel = WoodcuttingLevel,
			WoodcuttingExperience = WoodcuttingExperience
		};
		PlayerData.Save( playerData );
		_saveTimer = 0f;
	}

	private void LoadPlayerData()
	{
		var playerData = PlayerData.Load();
		if ( playerData == null )
		{
			var data = new PlayerData
			{
				MiningLevel = 1,
				MiningExperience = 0,
				WoodcuttingLevel = 1,
				WoodcuttingExperience = 0,
				Rocks = 0,
				Wood = 0,
			};
			PlayerData.Save( data );
		}
		else
		{
			Logs = playerData.Wood;
			Rocks = playerData.Rocks;
		}
	}

	private void InitializeComponents()
	{
		_citizenAnimationHelper = Player.Components.Get<CitizenAnimationHelper>();
	}

	private void ApplyClothing()
	{
		var model = Components.GetInChildren<SkinnedModelRenderer>();
		if ( model != null )
		{
			ClothingContainer = ClothingContainer.CreateFromLocalUser();
			ClothingContainer.Apply( model );
		}
	}

	private void HandleInput()
	{
		if ( Input.Down( "attack1" ) )
			TriggerWeaponAttack();

		if ( Input.Down( "attack2" ) )
		{
			TriggerWeaponIronsight();
		}
		else if ( WeaponViewModel != null )
		{
			WeaponViewModel.Set( "ironsights", 0 );
		}

		if ( Input.Pressed( "reload" ) )
			TryReloadWeapon();

		if ( _lastPunch >= PunchCooldown + 1 )
			IdleAnimation();

		HandleInventoryInput();
	}

	private void HandleInventoryInput()
	{
		if ( Input.MouseWheel.y >= 0 )
			ActiveSlot = (ActiveSlot + Math.Sign( Input.MouseWheel.y )) % Slots;
		else if ( Input.MouseWheel.y < 0 )
			ActiveSlot = ((ActiveSlot + Math.Sign( Input.MouseWheel.y )) % Slots) + Slots;
	}

	private void StartAction( bool isActive, float actionSpeed, long actionAmount, GenericColliderController.ActionType actionType )
	{
		if ( !isActive ) return;

		Timer += Time.Delta;
		_soundTimer += Time.Delta;

		if ( _soundTimer >= 1f )
		{
			PlaySound( actionType );
			Punch();
		}

		if ( Timer >= actionSpeed )
		{
			Timer = 0f;
			UpdateResource( actionAmount, actionType );
			Sound.Play( _resourceGained );
		}
	}

	private void UpdateResource( long amount, GenericColliderController.ActionType actionType )
	{
		if ( actionType == GenericColliderController.ActionType.Cutting )
		{
			Logs += amount;
			PlayerProgression.AddExperience( this, 25, actionType );
		}
		else if ( actionType == GenericColliderController.ActionType.Rocking )
		{
			Rocks += amount;
			PlayerProgression.AddExperience( this, 25, actionType );
		}
	}

	private void PlaySound( GenericColliderController.ActionType action )
	{
		switch ( action )
		{
			case GenericColliderController.ActionType.Cutting:
				Sound.Play( "impact-melee-wood" );
				break;
			case GenericColliderController.ActionType.Rocking:
				Sound.Play( "impact-melee-concrete" );
				break;
		}

		_soundTimer = 0f;
	}

	private void TriggerWeaponAttack()
	{
		if ( WeaponViewModel == null || _isReloading )
			return;

		if ( AmmoInMagazine <= 0 )
		{
			TriggerDryFire();
			return;
		}

		if ( _timeSinceLastShot < FireRate )
			return;

		AmmoInMagazine--;
		_timeSinceLastShot = 0f;

		WeaponViewModel.Set( "b_attack", true );
		WeaponViewModel.Set( "b_empty", AmmoInMagazine <= 0 );
		PlayWeaponSound( FireSound );
		AddCameraRecoil();
		SpawnMuzzleFlash();
		FireWeaponTrace();
	}

	private void TriggerDryFire()
	{
		if ( _timeSinceDryFire < 0.2f || WeaponViewModel == null )
			return;

		_timeSinceDryFire = 0f;
		WeaponViewModel.Set( "b_attack_dry", true );
		PlayWeaponSound( DryFireSound );
	}

	private void TryReloadWeapon()
	{
		if ( WeaponViewModel == null || _isReloading )
			return;

		if ( AmmoInMagazine >= MagazineSize || ReserveAmmo <= 0 )
			return;

		_isReloading = true;
		_reloadTimer = ReloadDuration;
		WeaponViewModel.Set( "b_reload", true );
		PlayWeaponSound( ReloadSound );
	}

	private void UpdateWeaponViewModel()
	{
		if ( WeaponViewModel == null )
			return;

		var isMoving =
			Input.Down( "forward" ) ||
			Input.Down( "backward" ) ||
			Input.Down( "left" ) ||
			Input.Down( "right" );

		WeaponViewModel.Set( "b_sprint", Input.Down( "run" ) && isMoving );
		WeaponViewModel.Set( "attack_hold", Input.Down( "attack1" ) && AmmoInMagazine > 0 && !_isReloading ? 1.0f : 0.0f );
		WeaponViewModel.Set( "b_empty", AmmoInMagazine <= 0 );

		if ( _isReloading )
		{
			_reloadTimer -= Time.Delta;
			if ( _reloadTimer <= 0f )
				FinishReload();
		}
	}

	private void InitializeWeaponViewModel()
	{
		if ( WeaponViewModel == null )
			return;

		WeaponViewModel.Set( "b_deploy_skip", true );
		WeaponViewModel.Set( "firing_mode", 3 );
		WeaponViewModel.Set( "reload_type", 1 );
		WeaponViewModel.Set( "b_empty", AmmoInMagazine <= 0 );
	}

	private void FinishReload()
	{
		var ammoNeeded = MagazineSize - AmmoInMagazine;
		var ammoToLoad = Math.Min( ammoNeeded, ReserveAmmo );

		AmmoInMagazine += ammoToLoad;
		ReserveAmmo -= ammoToLoad;
		_isReloading = false;
		WeaponViewModel.Set( "b_empty", AmmoInMagazine <= 0 );
	}

	private void AddCameraRecoil()
	{
		_cameraRecoilTarget += new Angles(
			-CameraRecoilPitch,
			Game.Random.Float( -CameraRecoilYaw, CameraRecoilYaw ),
			0f
		);
	}

	private void UpdateCameraRecoil()
	{
		_cameraRecoilTarget = Angles.Lerp( _cameraRecoilTarget, Angles.Zero, Time.Delta * CameraRecoilRecoverSpeed );
		_cameraRecoilCurrent = Angles.Lerp( _cameraRecoilCurrent, _cameraRecoilTarget, Time.Delta * CameraRecoilSnappiness );
	}

	private void FireWeaponTrace()
	{
		if ( PlayerCamera == null )
		{
			Log.Warning( "FireWeaponTrace: PlayerCamera is null" );
			return;
		}

		var start = PlayerCamera.WorldPosition;
		var end = start + PlayerCamera.WorldRotation.Forward * WeaponRange;

		SceneTraceResult = Scene.Trace.FromTo( start, end )
			.Size( 2f )
			.IgnoreGameObjectHierarchy( Player?.GameObject )
			.Run();

		DebugOverlay.Line( start, end, Color.Red, 1.0f );

		if ( !SceneTraceResult.Hit )
		{
			Log.Info( $"FireWeaponTrace: no hit from {start} to {end}" );
			return;
		}

		Log.Info( $"FireWeaponTrace: hit {SceneTraceResult.GameObject?.Name} at {SceneTraceResult.EndPosition}" );
		Log.Info( $"FireWeaponTrace: hit root {SceneTraceResult.GameObject?.Root?.Name}" );

		var npc = FindHitNpc( SceneTraceResult.GameObject );
		if ( npc != null )
		{
			Log.Info( $"FireWeaponTrace: resolved NPC owner {npc.GameObject.Name} from hit object {SceneTraceResult.GameObject?.Name}" );
			Log.Info( $"FireWeaponTrace: applying {WeaponDamage} damage to NPC {npc.GameObject.Name}" );
			npc.TakeDamage( WeaponDamage, SceneTraceResult.EndPosition, PlayerCamera.WorldRotation.Forward * 250f );
		}
		else
		{
			Log.Info( "FireWeaponTrace: hit object is not a WanderersNpc" );
		}
	}

	private WanderersNpc FindHitNpc( GameObject hitObject )
	{
		if ( hitObject == null )
			return null;

		return hitObject.Components.GetInAncestorsOrSelf<WanderersNpc>()
			?? hitObject.Root?.Components.Get<WanderersNpc>( FindMode.EverythingInSelf );
	}

	private void TriggerWeaponIronsight()
	{
		WeaponViewModel?.Set( "ironsights", 1 );
	}

	private void PlayWeaponSound( SoundEvent soundName )
	{
		if ( soundName is null )
			return;

		Sound.Play( soundName );
	}

	private void SpawnMuzzleFlash()
	{
		if ( MuzzleFlashPrefab == null )
			return;

		Transform spawnTransform;

		var muzzleAttachment = WeaponViewModel?.SceneModel.GetAttachment( "muzzle" );
		if ( muzzleAttachment.HasValue )
		{
			spawnTransform = muzzleAttachment.Value;
		}
		else
		{
			var origin = MuzzleFlashOrigin ?? WeaponViewModel?.GameObject ?? PlayerCamera;
			if ( origin == null )
				return;

			spawnTransform = origin.WorldTransform;
		}

		Log.Info( $"SpawnMuzzleFlash: spawning prefab {MuzzleFlashPrefab.Name} at {spawnTransform.Position}" );
		var flash = MuzzleFlashPrefab.Clone( spawnTransform );
		flash.Flags |= GameObjectFlags.NotNetworked;
		flash.DestroyAsync( 2f );
	}

	private void Punch()
	{
		var start = Player.WorldPosition + Vector3.Up * 67f;
		var forward = PlayerCamera.WorldRotation.Forward;
		var end = start + forward * 3000f;

		SceneTraceResult = Scene.Trace.FromTo( start, end )
			.Size( 10f )
			.WithAnyTags( "NPC" )
			.Run();

		DebugOverlay.Line( start, end, Color.Yellow );

		if ( SceneTraceResult.Hit )
		{
			Log.Info( SceneTraceResult.GameObject );
			var npc = FindHitNpc( SceneTraceResult.GameObject );

			if ( npc != null )
			{
				Log.Info( "Hit a Wanderer NPC!" );
				npc.Health -= 50;
				Log.Info( npc.Health );
			}
			else
			{
				Log.Info( "Hit something that's not a Wanderer NPC" );
			}

			Log.Info( "Hit something!" );
		}

		if ( _citizenAnimationHelper != null )
		{
			_citizenAnimationHelper.HoldType = CitizenAnimationHelper.HoldTypes.Punch;
			_citizenAnimationHelper.Target.Set( "b_attack", true );
			Sound.Play( new SoundEvent( "sounds/physics/phys-impact-meat-2.vsnd_c" ) { UI = true, Volume = 2 } );
		}

		_lastPunch = 0f;
	}

	private void IdleAnimation()
	{
		if ( _citizenAnimationHelper != null )
			_citizenAnimationHelper.HoldType = CitizenAnimationHelper.HoldTypes.None;
	}

	public void BuyUpgrade( string upgrade )
	{
		if ( upgrade == "Mining" )
		{
			Log.Info( "Trying to upgrade mining" );
			Rocks -= 1;
		}
		else if ( upgrade == "Woodcutting" )
		{
			Log.Info( "Trying to upgrade woodcutting" );
			Logs -= 1;
		}
	}
}


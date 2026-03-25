using System.Threading.Tasks;

public static partial class GameObjectExtensions
{
	public static async void DestroyAsync( this GameObject go, float time )
	{
		await Task.Delay( (int)(time * 1000.0f) );
		go.Destroy();
	}
}

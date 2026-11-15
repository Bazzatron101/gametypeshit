using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Authoring asset for one store that buys duplicates (ticket 22). Stores stand around the
    /// world; each pays its own share of an item's value. The guild's, in the hub, is the easiest to
    /// reach and pays the least.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Shop", fileName = "NewShop")]
    public sealed class ShopAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Stable, unique id")] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField, Tooltip("Where the store stands in the world (the hub's centre is set on the Hub asset)")] private Vector3 position;
        [SerializeField, Min(0.5f), Tooltip("How close a player stands to trade")] private float radius = 2.5f;
        [SerializeField, Range(1, 100), Tooltip("The share of an item's sell value this store pays")] private int sellPercent = 100;

        public string Id => id;
        public string DisplayName => displayName;

        public ShopDefinition ToDefinition() => new ShopDefinition
        {
            Id = id, Name = displayName, Position = new Vec3(position.x, 0f, position.z), Radius = radius, SellPercent = sellPercent,
        };

        /// <summary>For editor tooling that creates stores in code.</summary>
        public void Set(string shopId, string shopName, Vector3 at, float reach, int percent)
        {
            id = shopId;
            displayName = shopName;
            position = at;
            radius = reach;
            sellPercent = percent;
        }
    }
}

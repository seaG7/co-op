using NUnit.Framework;
using UnityEngine;
using Data.Configs;
using Data.Effects;

namespace CoOp.Tests.EditMode.Effects
{
    public class CatalogTests
    {
        [Test]
        public void Vfx_UnsetId_ReturnsNull()
        {
            var cat = ScriptableObject.CreateInstance<VfxCatalog>();
            Assert.IsNull(cat.Get(VfxId.SourceExplode));
        }

        [Test]
        public void Vfx_EntryWithNullPrefab_ReturnsNull()
        {
            var cat = ScriptableObject.CreateInstance<VfxCatalog>();
            cat.Entries = new[] { new VfxCatalog.Entry { Id = VfxId.SourceExplode, Prefab = null } };
            Assert.IsNull(cat.Get(VfxId.SourceExplode));
        }

        [Test]
        public void Sfx_EmptyClips_ReturnsNull()
        {
            var cat = ScriptableObject.CreateInstance<SfxCatalog>();
            cat.Entries = new[] { new SfxCatalog.Entry { Id = SfxId.WeaponFire, Clips = new AudioClip[0] } };
            Assert.IsNull(cat.Get(SfxId.WeaponFire));
        }

        [Test]
        public void Sfx_PickClip_WrapsIndex()
        {
            var a = AudioClip.Create("a", 1, 1, 8000, false);
            var b = AudioClip.Create("b", 1, 1, 8000, false);
            var e = new SfxCatalog.Entry { Id = SfxId.WeaponFire, Clips = new[] { a, b } };
            Assert.AreSame(a, SfxCatalog.PickClip(e, 0));
            Assert.AreSame(b, SfxCatalog.PickClip(e, 1));
            Assert.AreSame(a, SfxCatalog.PickClip(e, 2));
        }
    }
}

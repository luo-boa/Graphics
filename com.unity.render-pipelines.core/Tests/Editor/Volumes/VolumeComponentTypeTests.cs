using System;
using FsCheck;
using NUnit.Framework;

namespace UnityEngine.Rendering.Tests
{
    class VolumeComponentTypeTests
    {
        [OneTimeSetUp]
        public static void SetupFixture()
        {
            ArbX.Register();
        }

        [Test]
        public void OnlyAcceptsVolumeComponentTypes()
        {
            bool Property(Type type)
            {
                var success = VolumeComponentType.FromType(type, out var volumeType);
                var isVolumeType = type?.IsSubclassOf(typeof(VolumeComponent));

                var mustBeValid = isVolumeType == true;
                var isValid = success && volumeType.AsType() == type;
                var isInvalid = !success && volumeType == default;

                return mustBeValid && isValid || !mustBeValid && isInvalid;
            }

            Prop.ForAll(ArbX.CreateTypeArbitrary(), Property).QuickCheckThrowOnFailure();
        }
    }
}

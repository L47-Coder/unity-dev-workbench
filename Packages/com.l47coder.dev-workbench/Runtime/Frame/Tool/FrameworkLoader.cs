using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DevWorkbench
{
    /// <summary>
    /// Unified entry point for loading Addressable assets from Manager or
    /// gameplay code. Every load returns a freshly instantiated clone so the
    /// caller owns its own instance; the backing Addressable handle is released
    /// immediately afterwards.
    /// </summary>
    public static class FrameworkLoader
    {
        /// <summary>
        /// Asynchronously load the Addressable asset at <paramref name="address"/>
        /// and return an instantiated clone.
        /// </summary>
        /// <typeparam name="T">The concrete Unity asset type to load.</typeparam>
        /// <param name="address">The Addressable address of the asset.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="address"/> is null, empty or whitespace.</exception>
        /// <exception cref="Exception">Thrown when the Addressable load does not complete successfully.</exception>
        public static async UniTask<T> LoadAsync<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException($"Invalid Addressable address: {address}");

            var handle = Addressables.LoadAssetAsync<T>(address);
            await handle.ToUniTask();

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"Addressable load failed: {address}");

            var clone = UnityEngine.Object.Instantiate(handle.Result);
            Addressables.Release(handle);
            return clone;
        }

        /// <summary>
        /// Synchronously load the Addressable asset at <paramref name="address"/>
        /// and return an instantiated clone. Blocks the calling thread; prefer
        /// <see cref="LoadAsync{T}"/> for time-sensitive paths.
        /// </summary>
        /// <typeparam name="T">The concrete Unity asset type to load.</typeparam>
        /// <param name="address">The Addressable address of the asset.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="address"/> is null, empty or whitespace.</exception>
        /// <exception cref="Exception">Thrown when the Addressable load does not complete successfully.</exception>
        public static T LoadSync<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException($"Invalid Addressable address: {address}");

            var handle = Addressables.LoadAssetAsync<T>(address);
            handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"Addressable load failed: {address}");

            var clone = UnityEngine.Object.Instantiate(handle.Result);
            Addressables.Release(handle);
            return clone;
        }
    }
}

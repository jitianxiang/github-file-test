using System;

namespace CommonServiceLocator
{
    /// <summary>
    /// This class provides the ambient container for this application. If your
    /// framework defines such an ambient container, use ServiceLocator.Current
    /// to get it.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly object _providerLock = new object();
        private static ServiceLocatorProvider _currentProvider;
        private static ProviderScope _currentScope;

        /// <summary>
        /// The current ambient container.
        /// </summary>
        public static IServiceLocator Current
        {
            get
            {
                ServiceLocatorProvider provider;

                lock (_providerLock)
                {
                    provider = _currentProvider;
                }

                if (provider == null) throw new InvalidOperationException(" ServiceLocationProvider must be set.");

                return provider();
            }
        }

        /// <summary>
        /// Set the delegate that is used to retrieve the current container.
        /// </summary>
        /// <param name="newProvider">Delegate that, when called, will return
        /// the current ambient container.</param>
        /// <remarks>
        /// Setting a provider directly supersedes all scopes created by
        /// <see cref="PushLocatorProvider"/>. Disposing those scopes afterwards
        /// does not change the provider set here.
        /// </remarks>
        public static void SetLocatorProvider(ServiceLocatorProvider newProvider)
        {
            lock (_providerLock)
            {
                _currentScope = null;
                _currentProvider = newProvider;
            }
        }

        /// <summary>
        /// Installs a provider until the returned scope is disposed.
        /// </summary>
        /// <param name="newProvider">Delegate that, when called, will return
        /// the current ambient container.</param>
        /// <returns>A scope that restores the preceding provider when disposed.</returns>
        /// <remarks>
        /// Provider scopes are process-wide. Changes made by a scope are visible
        /// to all threads and asynchronous control flows; scopes do not use
        /// thread-local or execution-context-local storage. Scopes may be nested
        /// and disposed in any order.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="newProvider"/> is null.
        /// </exception>
        public static IDisposable PushLocatorProvider(ServiceLocatorProvider newProvider)
        {
            if (newProvider == null) throw new ArgumentNullException(nameof(newProvider));

            lock (_providerLock)
            {
                var scope = new ProviderScope(_currentProvider, _currentScope);
                _currentScope = scope;
                _currentProvider = newProvider;
                return scope;
            }
        }

        /// <summary>
        /// Gets a value indicating whether an ambient provider is installed.
        /// </summary>
        public static bool IsLocationProviderSet
        {
            get
            {
                lock (_providerLock)
                {
                    return _currentProvider != null;
                }
            }
        }

        private sealed class ProviderScope : IDisposable
        {
            private readonly ServiceLocatorProvider _previousProvider;
            private readonly ProviderScope _previousScope;
            private bool _isDisposed;

            public ProviderScope(
                ServiceLocatorProvider previousProvider,
                ProviderScope previousScope)
            {
                _previousProvider = previousProvider;
                _previousScope = previousScope;
            }

            public void Dispose()
            {
                lock (_providerLock)
                {
                    if (_isDisposed) return;

                    _isDisposed = true;
                    if (!ReferenceEquals(_currentScope, this)) return;

                    ProviderScope scope = _previousScope;
                    ServiceLocatorProvider provider = _previousProvider;

                    while (scope != null && scope._isDisposed)
                    {
                        provider = scope._previousProvider;
                        scope = scope._previousScope;
                    }

                    _currentScope = scope;
                    _currentProvider = provider;
                }
            }
        }
    }
}

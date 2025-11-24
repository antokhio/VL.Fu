using System.Reactive.Disposables;
using VL.Fu.Core.Context;
using VL.Fu.Core.Input;

namespace VL.Fu.Core.Services
{
    public class InteractionService : IContextedService, IInteractionService
    {
        private readonly IContextProvider _provider;
        private readonly CompositeDisposable _subscriptions = new();

        public InteractionService(
            IContextProvider provider,
            INotificationsService notificationsService
        )
        {
            _provider = provider;
            _subscriptions.Add(notificationsService.InputStateStream.Subscribe(OnUpdateInput));
        }

        public void OnUpdateInput(FuInputState inputState)
        {
            // Active interactions

            // Discover new interactions
            // root = _provider.Root;
            // foreach (node)
            // node.TryActivate(inputState);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}

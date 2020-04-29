using System;
using System.Threading;
using System.Threading.Tasks;
using MiaPlaza.MailService.Delivery;
using MiaPlaza.MailService.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace MiaPlaza.MailService {

	public class MessageProcessor : IMessageProcessor {

		private readonly IMailDeliverer deliverer;
		private readonly IMailStorage storage;
		private readonly ILogger logger;
		private readonly IDistributedCache cache;

		public MessageProcessor(IMailDeliverer deliverer, IMailStorage storage, IDistributedCache cache, ILogger<MessageProcessor> logger) {
			this.deliverer = deliverer;
			this.storage = storage;
			this.logger = logger;
			this.cache = cache;
		}
		public async Task ProcessAsync(EmailMessage emailMessage) {
			var cached = await cache.GetStringAsync(emailMessage.Id.ToString());
			if(cached != null) {
				logger.LogInformation($"Caught duplicate email {emailMessage.Id}");
				return;
			}

			await tryStoreOrLogAsync(() => storage.SetProcessedAsync(emailMessage));
			try {
				await deliverer.DeliverAsync(emailMessage);
			} catch (Exception e){
				await tryStoreOrLogAsync(() => storage.SetErrorAsync(emailMessage, e.Message));
				throw e;
			}
			
			await cache.SetStringAsync(emailMessage.Id.ToString(), "t");
			await tryStoreOrLogAsync(() => storage.SetSentAsync(emailMessage));
		}

		private async Task tryStoreOrLogAsync(Func<Task> storeFunc) {
			try {
				await storeFunc();
			} catch(Exception e) {
				logger.LogError(e, "Exception while storing EmailMessage.");
			}
		}
	}
}

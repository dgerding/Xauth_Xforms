using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using XAuth_XForms.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using Microsoft.WindowsAzure.MobileServices.Sync;

namespace XAuth_XForms.Services
{
	public class AzureDataStore : IDataStore<Item>
	{
        bool isInitialized;
		IMobileServiceSyncTable<Item> itemsTable;

		public MobileServiceClient MobileService { get; set; }

		public async Task<IEnumerable<Item>> GetItemsAsync(bool forceRefresh = false)
		{
			await InitializeAsync();
			if (forceRefresh)
				await PullLatestAsync();

			return await itemsTable.ToEnumerableAsync();
		}

		public async Task<Item> GetItemAsync(string id)
		{
			await InitializeAsync();
			await PullLatestAsync();
			var items = await itemsTable.Where(s => s.Id == id).ToListAsync();

			if (items == null || items.Count == 0)
				return null;

			return items[0];
		}

		public async Task<bool> AddItemAsync(Item item)
		{
			await InitializeAsync();
			await PullLatestAsync();
			await itemsTable.InsertAsync(item);
			await SyncAsync();

			return true;
		}

		public async Task<bool> UpdateItemAsync(Item item)
		{
			await InitializeAsync();
			await itemsTable.UpdateAsync(item);
			await SyncAsync();

			return true;
		}

		public async Task<bool> DeleteItemAsync(Item item)
		{
			await InitializeAsync();
			await PullLatestAsync();
			await itemsTable.DeleteAsync(item);
			await SyncAsync();

			return true;
		}

		public async Task InitializeAsync()
		{
			if (isInitialized)
				return;

			MobileService = new MobileServiceClient(App.AzureMobileAppUrl)
			{
				SerializerSettings = new MobileServiceJsonSerializerSettings
				{
					CamelCasePropertyNames = true
				}
			};

            var path = "syncstore.db";
            path = Path.Combine(MobileServiceClient.DefaultDatabasePath, path);
            var store = new MobileServiceSQLiteStore(path);

			store.DefineTable<Item>();
			await MobileService.SyncContext.InitializeAsync(store, new MobileServiceSyncHandler());
			itemsTable = MobileService.GetSyncTable<Item>();

			isInitialized = true;
		}

		public async Task<bool> PullLatestAsync()
		{
			try
			{
				await itemsTable.PullAsync($"all{typeof(Item).Name}", itemsTable.CreateQuery());
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Unable to pull items: {ex.Message}");

				return false;
			}
			return true;
		}

		public async Task<bool> SyncAsync()
		{
			try
			{
				await MobileService.SyncContext.PushAsync();
				if (!(await PullLatestAsync().ConfigureAwait(false)))
					return false;
			}
			catch (MobileServicePushFailedException exc)
			{
				if (exc.PushResult == null)
				{
					Debug.WriteLine($"Unable to sync, that is alright as we have offline capabilities: {exc.Message}");

					return false;
				}
				foreach (var error in exc.PushResult.Errors)
				{
					if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Result != null)
					{
						//Update failed, reverting to server's copy.
						await error.CancelAndUpdateItemAsync(error.Result);
					}
					else
					{
						// Discard local change.
						await error.CancelAndDiscardItemAsync();
					}

					Debug.WriteLine($"Error executing sync operation. Item: {error.TableName} ({error.Item["id"]}). Operation discarded.");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Unable to sync items: {ex.Message}");
				return false;
			}

			return true;
		}
	}
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TaintedCain
{
	public class ItemManager
	{
		private static readonly string DataFolder = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
		private static (string name, (int id, float weight)[] items)[] ItemPools { get; }
		private static Dictionary<int, int> ItemQualities { get; }
		
		public static Dictionary<int, string> ItemNames { get; }
		public static Dictionary<int, string> ItemDescriptions { get; }
		
		public ObservableCollection<Item> Items { get; } = new ObservableCollection<Item>();
		public ObservableCollection<Pickup> Pickups { get; } = new ObservableCollection<Pickup>();

		static ItemManager()
		{
			ItemPools =
				XElement.Load(DataFolder + "itempools.xml")
					.XPathSelectElements("Pool")
					.Select(e => (
						e.Attribute("Name").Value,
						e.Elements("Item").Select(x => (Convert.ToInt32(x.Attribute("Id").Value),
							Convert.ToSingle(x.Attribute("Weight").Value))).ToArray()))
					.ToArray();

			ItemQualities =
				XElement.Load(DataFolder + "items_metadata.xml")
					.XPathSelectElements("item")
					.ToDictionary(e => Convert.ToInt32(e.Attribute("id").Value),
						e => Convert.ToInt32(e.Attribute("quality").Value));

			ItemNames =
				XElement.Load(DataFolder + "items.xml")
					.Elements()
					.ToDictionary(e => Convert.ToInt32(e.Attribute("id").Value),
						e => e.Attribute("name").Value);
			
			ItemDescriptions =
				XElement.Load(DataFolder + "items.xml")
					.Elements()
					.ToDictionary(e => Convert.ToInt32(e.Attribute("id").Value),
						e => e.Attribute("description").Value);
		}

		public ItemManager()
		{
			for (int i = 1; i <= Pickup.Names.Length; i++)
			{
				Pickup pickup = new Pickup(i);
				pickup.PropertyChanged += PickupOnPropertyChanged;
				Pickups.Add(pickup);
			}
		}

		public void Clear()
		{
			foreach (Pickup pickup in Pickups)
			{
				pickup.PropertyChanged -= PickupOnPropertyChanged;
				pickup.Amount = 0;
				pickup.PropertyChanged += PickupOnPropertyChanged;
			}

			Items.Clear();
		}

		private void PickupOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (!e.PropertyName.Equals("Amount"))
			{
				return;
			}
			
			Pickup changed_pickup = (Pickup) sender;
			var args = (PropertyChangedExtendedEventArgs<int>) e;
			
			if (args.NewValue < args.OldValue)
			{
				RemoveRecipes(changed_pickup);
			}
			else if(args.NewValue > args.OldValue)
			{
				AddRecipes(changed_pickup.Id, args.OldValue);
			}
		}

		private void AddRecipes(int changed_id, int old_amount)
		{
			void AddRecipesHelper(List<Pickup> current_recipe, int pickup_index, int prev_length)
			{
				Pickup current_pickup = current_recipe[pickup_index];
				current_pickup.Amount = Math.Min(8 - prev_length, Pickups[pickup_index].Amount);

				if (prev_length + current_pickup.Amount == 8)
				{
					AddCraft(current_recipe);

					current_pickup.Amount -= 1;
				}

				if (pickup_index == current_recipe.Count - 1)
				{
					return;
				}

				int minimum = pickup_index + 1 == changed_id ? old_amount + 1 : 0;
				
				while (current_pickup.Amount >= minimum)
				{
					AddRecipesHelper(current_recipe, pickup_index + 1, prev_length + current_pickup.Amount);
					current_pickup.Amount -= 1;
				}

				current_pickup.Amount = 0;
			}

			List<Pickup> empty_recipe = new List<Pickup>();

			for (int i = 1; i <= Pickup.Names.Length; i++)
			{
				empty_recipe.Add(new Pickup(i));
			}

			AddRecipesHelper(empty_recipe, 0, 0);
		}
		
		private void AddCraft(List<Pickup> recipe)
		{
			List<Pickup> recipe_copy = new List<Pickup>();
			List<int> crafting_array = new List<int>();

			foreach (Pickup p in recipe)
			{
				if (p.Amount == 0) continue;

				recipe_copy.Add(new Pickup(p.Id, p.Amount));

				for (int i = 0; i < p.Amount; i++)
				{
					crafting_array.Add(p.Id);
				}
			}

			int item_id = Crafting.CalculateCrafting(crafting_array.ToArray(), ItemPools, ItemQualities);

			Item existing_item = Items.FirstOrDefault(i => i.Id == item_id);
			if (existing_item == null)
			{
				Item item = new Item(item_id, 
					ItemNames.GetValueOrDefault(item_id), 
					ItemDescriptions.GetValueOrDefault(item_id));
				item.Recipes.Add(recipe_copy);
				Items.Add(item);
			}
			else
			{
				existing_item.Recipes.Add(recipe_copy);
			}
		}

		private void RemoveRecipes(Pickup changed_pickup)
		{
			for (int i = 0; i < Items.Count; i++)
			{
				Item item = Items[i];

				RemoveItemRecipes(item, changed_pickup);

				if (item.Recipes.Count == 0)
				{
					Items.RemoveAt(i);
					i--;
				}
			}
		}
			
		private void RemoveItemRecipes(Item item, Pickup changed_pickup)
		{
			for (int j = 0; j < item.Recipes.Count; j++)
			{
				Pickup existing = item.Recipes[j].FirstOrDefault(p => p.Id == changed_pickup.Id);

				if (existing == null)
				{
					continue;
				}

				if (existing.Amount > changed_pickup.Amount)
				{
					item.Recipes.RemoveAt(j);
					j--;
				}
			}
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace Agility.Web.AgilityContentServer
{
	public partial class AgilityExperimentListing
	{
		
		public void Merge(AgilityExperimentListing delta)
		{
			if (Items == null) Items = new AgilityExperiment[0];

			Dictionary<int, AgilityExperiment> items = Items.ToDictionary(i => i.ID);

			if (delta != null)
			{
				foreach (var ex in delta.Items)
				{
					if (ex.Deleted)
					{
						items.Remove(ex.ID);
					}
					else
					{
						items[ex.ID] = ex;
					}
				}

				this.LastAccessDate = delta.LastAccessDate;
			}

			Items = items.Values.ToArray();
		}

		public AgilityExperiment GetExperiment(int experimentID)
		{

			if (Items == null || Items.Length == 0) return null;

			AgilityExperiment exp = Items.FirstOrDefault(e => e.ID == experimentID);
			
			if (exp != null && exp.IsCurrent)
			{
				return exp;
			}

			return null;			
		}

		public AgilityExperiment GetForPage(int pageID)
		{
			if (Items == null || Items.Length == 0) return null;

			AgilityExperiment exp = Items.Where(e => e.PageID == pageID && e.IsCurrent).OrderBy(e => e.ID).FirstOrDefault();
			
			return exp;
		}
	}

	public partial class AgilityExperiment
	{
		private static Random random = new Random(DateTime.Now.Millisecond);

		public bool IsCurrent
		{
			get
			{
				DateTime nowDate = DateTime.Now;

				if (!Deleted
					&& Enabled
					&& (StartDateTime == null || StartDateTime <= nowDate)
					&& (EndDateTime == null || EndDateTime >= nowDate)
				)
				{
					return true;
				}

				return false;

			}
		}

	}
}

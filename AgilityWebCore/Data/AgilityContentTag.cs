namespace Agility.Web
{
	public class AgilityContentTag
	{
		string _tag;

		public string Tag
		{
			get { return _tag; }
			set { _tag = value; }
		}
		int _tagID;

		public int TagID
		{
			get { return _tagID; }
			set { _tagID = value; }
		}

		public override string ToString()
		{
			return _tag;
		}
	}
}

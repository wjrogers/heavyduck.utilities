using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace HeavyDuck.Utilities.Collections
{
    /// <summary>
    /// Provides support for binding a list of objects of any type to a control, including filtering.
    /// </summary>
    /// <typeparam name="T">The type of object the list will contain.</typeparam>
    public class ObjectBindingList<T> : IList<T>, ICollection<T>, IBindingList, IBindingListView, ITypedList
    {
        private readonly PropertyDescriptorCollection m_props = TypeDescriptor.GetProperties(typeof(T), new Attribute[] { new BrowsableAttribute(true) }).Sort();
        private string m_filter = null;
        private bool m_implements_filterable = typeof(T).GetInterface("IFilterable") != null;
        private List<T> m_list_actual;
        private List<T> m_list_current;
        private ListSortDescriptionCollection m_sorts = null;

        /// <summary>
        /// Creates a new instance of ObjectBindingList.
        /// </summary>
        public ObjectBindingList()
        {
            m_list_actual = new List<T>();
            m_list_current = new List<T>();
        }

        /// <summary>
        /// Creates a new instance of ObjectBindingList.
        /// </summary>
        /// <param name="list">The initial set of elements.</param>
        public ObjectBindingList(IEnumerable<T> list)
        {
            m_list_actual = new List<T>(list);
            m_list_current = new List<T>(list);
        }

        /// <summary>
        /// Updates the current version of the list, re-applying filter and sort parameters.
        /// </summary>
        /// <param name="filterChanged">Indicates whether the filter has changed.</param>
        protected void UpdateCurrentList(bool filterChanged)
        {
            // build current list based on filter
            if (filterChanged && IsFiltered)
            {
                // create a new list to keep the items currently selected by the filter
                m_list_current = new List<T>(m_list_actual.Count);

                // test each item to see whether it passes
                foreach (T item in m_list_actual)
                {
                    if (TestObject(item))
                        m_list_current.Add(item);
                }
            }
            else if (filterChanged)
            {
                // changed from filtered to un-filtered, load actual list as current
                m_list_current = new List<T>(m_list_actual);
            }

            // apply sort
            if (IsSorted)
            {
                m_list_current.Sort(CompareObjects);
            }

            // raise the list changed event to signal the list has changed significantly
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected bool TestObject(T item)
        {
            // if the type of item in our list implements IFilterable, use that interface to test
            if (m_implements_filterable)
                return (item as IFilterable).Test(m_filter);
            else
                return item.ToString().ToLower().Contains(m_filter);
        }

        protected int CompareObjects(T a, T b)
        {
            int c;
            Comparer comp = Comparer.Default;

            foreach (ListSortDescription lsd in m_sorts)
            {
                // compare property
                c = comp.Compare(lsd.PropertyDescriptor.GetValue(a), lsd.PropertyDescriptor.GetValue(b));
                if (lsd.SortDirection == ListSortDirection.Descending)
                    c = -c;

                // if there was a difference, return
                if (c != 0)
                    return c;
            }

            // they are equal!
            return 0;
        }

        /// <summary>
        /// Adds an object to the current list, taking filter and sort into account.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <returns>The index at which the item was inserted.</returns>
        protected int AddItemCurrent(T item)
        {
            // only add item if we are not filtered or the item passes the filter
            if (!IsFiltered || TestObject(item))
            {
                // loop through the current list looking for the correct insertion point
                if (IsSorted)
                {
                    for (int i = 0; i < m_list_current.Count; ++i)
                    {
                        if (CompareObjects(m_list_current[i], item) > 0)
                        {
                            m_list_current.Insert(i, item);
                            return i;
                        }
                    }
                }

                // this guy goes to the end of the line
                m_list_current.Add(item);
                return m_list_current.Count - 1;
            }

            // we didn't add the item at all
            return -1;
        }

        /// <summary>
        /// Raises the ListChanged event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected void OnListChanged(ListChangedEventArgs e)
        {
            if (ListChanged != null) ListChanged(this, e);
        }

        /// <summary>
        /// Sorts the list by a named property.
        /// </summary>
        /// <param name="propertyName">The name of the property to sort by.</param>
        /// <param name="direction">The sort direction.</param>
        public void ApplySort(string propertyName, ListSortDirection direction)
        {
            // search for a matching PropertyDescriptor
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(typeof(T)))
            {
                if (property.Name == propertyName)
                {
                    this.ApplySort(property, direction);
                    return;
                }
            }

            // none found, bad argument
            throw new ArgumentException(string.Format("Type '{0}' has no property '{1}'", typeof(T).Name, propertyName));
        }

        public T[] ToArray()
        {
            return m_list_current.ToArray();
        }

        public void AddRange(IEnumerable<T> items)
        {
            // add items to actual list, easy
            m_list_actual.AddRange(items);

            // add items to current list, potentially complicated
            if (IsFiltered || IsSorted)
            {
                foreach (T item in items)
                    AddItemCurrent(item);
            }
            else
            {
                m_list_current.AddRange(items);
            }

            // notify that shit is off the hook
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        public bool IsFiltered
        {
            get { return m_filter != null; }
        }

        #region IList<T> Members

        public int IndexOf(T item)
        {
            return m_list_current.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            if (IsFiltered || IsSorted) throw new InvalidOperationException("Cannot insert items while the list is filtered or sorted");
            m_list_actual.Insert(index, item);
            m_list_current.Insert(index, item);
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, index));
        }

        public void RemoveAt(int index)
        {
            if (IsFiltered || IsSorted) throw new InvalidOperationException("Cannot remove items by index while the list is filtered or sorted");
            m_list_actual.RemoveAt(index);
            m_list_current.RemoveAt(index);
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
        }

        public T this[int index]
        {
            get
            {
                return m_list_current[index];
            }
            set
            {
                if (IsFiltered || IsSorted) throw new InvalidOperationException("Cannot alter items by index while the list is filtered or sorted");
                m_list_actual[index] = value;
                m_list_current[index] = value;
                OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
            }
        }

        #endregion

        #region ICollection<T> Members

        public void Add(T item)
        {
            m_list_actual.Add(item);
            int i = AddItemCurrent(item);
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, i));
        }

        public void Clear()
        {
            m_list_actual.Clear();
            m_list_current.Clear();
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        public bool Contains(T item)
        {
            return m_list_current.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_list_current.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return m_list_current.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            // find a matching item in the current list, return false if none exists
            int index = m_list_current.IndexOf(item);
            if (index == -1) return false;

            // remove the item, notify, and return true
            m_list_current.RemoveAt(index);
            m_list_actual.Remove(item);
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
            return true;
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            return m_list_current.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion

        #region IList Members

        int IList.Add(object value)
        {
            this.Add((T)value);
            return this.Count - 1;
        }

        void IList.Clear()
        {
            this.Clear();
        }

        bool IList.Contains(object value)
        {
            return this.Contains((T)value);
        }

        int IList.IndexOf(object value)
        {
            return this.IndexOf((T)value);
        }

        void IList.Insert(int index, object value)
        {
            this.Insert(index, (T)value);
        }

        bool IList.IsFixedSize
        {
            get { return false; }
        }

        bool IList.IsReadOnly
        {
            get { return this.IsReadOnly; }
        }

        void IList.Remove(object value)
        {
            this.Remove((T)value);
        }

        void IList.RemoveAt(int index)
        {
            this.RemoveAt(index);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (T)value; }
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index)
        {
            (m_list_current as ICollection).CopyTo(array, index);
        }

        int ICollection.Count
        {
            get { return this.Count; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get { return null; }
        }

        #endregion

        #region IBindingList Members

        void IBindingList.AddIndex(PropertyDescriptor property)
        {
            // pass
        }

        object IBindingList.AddNew()
        {
            throw new NotSupportedException();
        }

        bool IBindingList.AllowEdit
        {
            get { return !this.IsReadOnly; }
        }

        bool IBindingList.AllowNew
        {
            get { return false; }
        }

        bool IBindingList.AllowRemove
        {
            get { return !this.IsReadOnly; }
        }

        public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
        {
            this.ApplySort(new ListSortDescriptionCollection(new ListSortDescription[] { new ListSortDescription(property, direction) }));
        }

        int IBindingList.Find(PropertyDescriptor property, object key)
        {
            throw new NotSupportedException();
        }

        public bool IsSorted
        {
            get { return m_sorts != null; }
        }

        public event ListChangedEventHandler ListChanged;

        void IBindingList.RemoveIndex(PropertyDescriptor property)
        {
            // pass
        }

        public void RemoveSort()
        {
            m_sorts = null;
            UpdateCurrentList(false);
        }

        public ListSortDirection SortDirection
        {
            get
            {
                if (!IsSorted) throw new InvalidOperationException("List is not currently sorted");
                return m_sorts[0].SortDirection;
            }
        }

        public PropertyDescriptor SortProperty
        {
            get
            {
                if (!IsSorted) throw new InvalidOperationException("List is not currently sorted");
                return m_sorts[0].PropertyDescriptor;
            }
        }

        bool IBindingList.SupportsChangeNotification
        {
            get { return true; }
        }

        bool IBindingList.SupportsSearching
        {
            get { return false; }
        }

        bool IBindingList.SupportsSorting
        {
            get { return true; }
        }

        #endregion

        #region IBindingListView Members

        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            m_sorts = sorts;
            UpdateCurrentList(false);
        }

        public string Filter
        {
            get
            {
                return m_filter;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    RemoveFilter();
                }
                else
                {
                    m_filter = value.ToLower();
                    UpdateCurrentList(true);
                }
            }
        }

        public void RemoveFilter()
        {
            m_filter = null;
            UpdateCurrentList(true);
        }

        public ListSortDescriptionCollection SortDescriptions
        {
            get { return m_sorts; }
        }

        bool IBindingListView.SupportsAdvancedSorting
        {
            get { return true; }
        }

        bool IBindingListView.SupportsFiltering
        {
            get { return true; }
        }

        #endregion

        #region ITypedList Members

        PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            return m_props;
        }

        string ITypedList.GetListName(PropertyDescriptor[] listAccessors)
        {
            return typeof(T).Name;
        }

        #endregion
    }

    /// <summary>
    /// Provides a method for testing an object using a string filter.
    /// </summary>
    public interface IFilterable
    {
        /// <summary>
        /// Tests whether this object matches the filter.
        /// </summary>
        /// <param name="filter">The filter to test.</param>
        bool Test(string filter);
    }
}

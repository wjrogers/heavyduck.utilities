using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HeavyDuck.Utilities.Collections
{
    /// <summary>
    /// Provides support for binding a list of objects of any type to a control, including filtering.
    /// </summary>
    /// <typeparam name="T">The type of object the list will contain.</typeparam>
    public class ObjectBindingList<T> : IList<T>, ICollection<T>, IComparer<T>, IBindingList, IBindingListView, ITypedList
    {
        private readonly PropertyDescriptorCollection m_props = TypeDescriptor.GetProperties(typeof(T), new Attribute[] { new BrowsableAttribute(true) }).Sort();
        private Func<T, bool> m_filter;
        private string m_filter_string;
        private bool m_implements_filterable = typeof(T).GetInterface("IFilterable") != null;
        private List<T> m_list_actual;
        private List<T> m_list_current;
        private ListSortDescriptionCollection m_sorts = null;
        private List<Tuple<ListSortDirection, Func<T, object>>> m_sorts_delegates;

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
            m_list_current = new List<T>(m_list_actual);
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
                m_list_current = m_list_actual.Where(m_filter).ToList();
            }
            else if (filterChanged)
            {
                // changed from filtered to un-filtered, load actual list as current
                m_list_current = new List<T>(m_list_actual);
            }

            // apply sort
            if (IsSorted)
                m_list_current.Sort(CompareObjects);

            // raise the list changed event to signal the list has changed significantly
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected int CompareObjects(T a, T b)
        {
            int c;
            Comparer comp = Comparer.Default;

            // use delegates for performance
            foreach (var sort in m_sorts_delegates)
            {
                // compare property
                c = comp.Compare(sort.Item2(a), sort.Item2(b));

                // if there was a difference, return
                if (c != 0)
                    return sort.Item1 == ListSortDirection.Descending ? -c : c;
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
            if (!IsFiltered || m_filter(item))
            {
                // loop through the current list looking for the correct insertion point
                if (IsSorted)
                {
                    int i = m_list_current.BinarySearch(item, this);

                    // don't care if there is already an equivalent item or not; insert at the returned point
                    if (i < 0) i = ~i;
                    m_list_current.Insert(i, item);

                    // return where we put it
                    return i;
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

        /// <summary>
        /// Applies a predicate-based filter to the list.
        /// </summary>
        /// <param name="filter">The new filter.</param>
        /// <param name="forceRefresh">If true, the list will be re-filtered even if the value of the filter has not changed.</param>
        public void ApplyFilter(Func<T, bool> filter, bool forceRefresh = false)
        {
            if (m_filter != filter || forceRefresh)
            {
                m_filter = filter;
                m_filter_string = null;
                UpdateCurrentList(true);
            }
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

        /// <summary>
        /// Creates a generic delegate for any property-get method.
        /// </summary>
        /// <typeparam name="TTarget">The type to which the property belongs.</typeparam>
        /// <param name="propertyGet">The property-get method.</param>
        /// <remarks>
        /// Creating a delegate improves performance by an order of magnitude over setting the property value with reflection.
        /// See: http://msmvps.com/blogs/jon_skeet/archive/2008/08/09/making-reflection-fly-and-exploring-delegates.aspx
        /// </remarks>
        private static Func<TTarget, object> CreatePropertyGetDelegate<TTarget>(MethodInfo propertyGet)
        {
            // get the generic helper method
            MethodInfo genericHelper = typeof(ObjectBindingList<T>).GetMethod("CreatePropertyGetDelegateHelper", BindingFlags.Static | BindingFlags.NonPublic);

            // supply type arguments
            MethodInfo constructedHelper = genericHelper.MakeGenericMethod(typeof(TTarget), propertyGet.ReturnType);

            // call it and return the resulting delegate
            return (Func<TTarget, object>)constructedHelper.Invoke(null, new object[] { propertyGet });
        }

        /// <summary>
        /// Converts a strongly-typed property-get delegate to a loosely-typed Func&lt;TTarget, object&gt; delegate.
        /// </summary>
        /// <typeparam name="TTarget">The type to which the property belongs.</typeparam>
        /// <typeparam name="TParam">The type of the property value.</typeparam>
        /// <param name="propertyGet">The property-get method.</param>
        /// <remarks>This helper is necessary because we don't know the type of the property value at compile time.</remarks>
        private static Func<TTarget, object> CreatePropertyGetDelegateHelper<TTarget, TParam>(MethodInfo propertyGet)
        {
            // convert the slow MethodInfo into a fast, strongly typed, open delegate
            Func<TTarget, TParam> func = (Func<TTarget, TParam>)Delegate.CreateDelegate(typeof(Func<TTarget, TParam>), propertyGet);

            // now create a more weakly typed delegate which will call the strongly typed one
            return (TTarget target) => func(target);
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
            get { return SortDescriptions != null; }
        }

        public event ListChangedEventHandler ListChanged;

        void IBindingList.RemoveIndex(PropertyDescriptor property)
        {
            // pass
        }

        public void RemoveSort()
        {
            SortDescriptions = null;
            UpdateCurrentList(false);
        }

        public ListSortDirection SortDirection
        {
            get
            {
                if (!IsSorted) throw new InvalidOperationException("List is not currently sorted");
                return SortDescriptions[0].SortDirection;
            }
        }

        public PropertyDescriptor SortProperty
        {
            get
            {
                if (!IsSorted) throw new InvalidOperationException("List is not currently sorted");
                return SortDescriptions[0].PropertyDescriptor;
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
            SortDescriptions = sorts;
            UpdateCurrentList(false);
        }

        string IBindingListView.Filter
        {
            get
            {
                return m_filter_string;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    RemoveFilter();
                }
                else if (m_filter_string != value)
                {
                    m_filter_string = value.ToLowerInvariant();
                    m_filter = t => t.ToString().ToLowerInvariant().Contains(m_filter_string);
                    UpdateCurrentList(true);
                }
            }
        }

        public void RemoveFilter()
        {
            ApplyFilter(null);
        }

        public ListSortDescriptionCollection SortDescriptions
        {
            get { return m_sorts; }
            private set
            {
                List<Tuple<ListSortDirection, Func<T, object>>> delegates = null;
                Func<T, object> getter;

                // don't do anything if the new one is the same as the old one
                if (value == m_sorts) return;

                // create property getter delegates for performance
                if (value != null)
                {
                    delegates = new List<Tuple<ListSortDirection, Func<T, object>>>(value.Count);

                    foreach (ListSortDescription lsd in value)
                    {
                        getter = CreatePropertyGetDelegate<T>(typeof(T).GetProperty(lsd.PropertyDescriptor.Name).GetGetMethod());
                        delegates.Add(Tuple.Create(lsd.SortDirection, getter));
                    }
                }

                // assign
                m_sorts = value;
                m_sorts_delegates = delegates;
            }
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

        #region IComparer<T> Members

        public int Compare(T x, T y)
        {
            return CompareObjects(x, y);
        }

        #endregion
    }
}

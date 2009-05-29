using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HeavyDuck.Utilities.Forms
{
    public delegate void ProgressTask(IProgressDialog dialog);

    public class ProgressException : Exception
    {
        public ProgressException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ProgressDialog : IProgressDialog
    {
        private ProgressForm m_form;
        private List<ProgressTask> m_tasks;
        private EventWaitHandle m_wait;
        private Exception m_exception;

        public ProgressDialog()
        {
            m_wait = new EventWaitHandle(false, EventResetMode.AutoReset);
            m_form = new ProgressForm(m_wait);
            m_tasks = new List<ProgressTask>();

            this.AutoAdvance = false;
        }

        public bool AutoAdvance { get; set; }

        public void AddTask(ProgressTask task)
        {
            m_tasks.Add(task);
        }

        public void Update(string message)
        {
            MethodInvoker code = delegate() { m_form.MessageText = message; };

            MarshalToForm(code);
        }

        public void Update(string message, int value, int max)
        {
            MethodInvoker code = delegate()
            {
                m_form.MessageText = message;
                m_form.ProgressBar.Value = value;
                m_form.ProgressBar.Maximum = max;
            };

            MarshalToForm(code);
        }

        public void Update(int value, int max)
        {
            MethodInvoker code = delegate()
            {
                m_form.ProgressBar.Value = value;
                m_form.ProgressBar.Maximum = max;
            };

            MarshalToForm(code);
        }

        public void Advance()
        {
            Advance(1);
        }

        public void Advance(int step)
        {
            MethodInvoker code = delegate() { m_form.ProgressBar.Value += step; };

            MarshalToForm(code);
        }

        private void MarshalToForm(MethodInvoker code)
        {
            if (m_form.InvokeRequired)
                m_form.BeginInvoke(code);
            else
                code();
        }

        public void Show()
        {
            Thread t;

            // make sure that exception is null
            m_exception = null;

            // create and launch the thread that will run stuff
            t = new Thread(RunTasks);
            t.IsBackground = true;
            t.Start();

            // when auto-advancing, set the parameters
            if (AutoAdvance)
                Update(0, m_tasks.Count);

            // show the dialog
            m_form.ShowDialog();

            // throw an exception if something bad happened while running the tasks
            if (m_exception != null) throw new ProgressException("Background task failed", m_exception);
        }

        private void RunTasks()
        {
            // wait for the form to signal that it is visible
            m_wait.WaitOne();

            // run the tasks
            try
            {
                foreach (ProgressTask task in m_tasks)
                {
                    task(this);

                    if (AutoAdvance)
                        Advance();
                }
            }
            catch (Exception ex)
            {
                m_exception = ex;
            }

            // close the form
            m_form.BeginInvoke(new MethodInvoker(m_form.Close));
        }
    }

    public interface IProgressDialog
    {
        void Update(string message);
        void Update(string message, int value, int max);
        void Update(int value, int max);
        void Advance();
        void Advance(int step);
    }
}

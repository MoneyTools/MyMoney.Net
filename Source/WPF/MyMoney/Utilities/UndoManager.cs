using System.Collections;

namespace Walkabout.Utilities
{
    public class UndoManager
    {
        ArrayList stack = new ArrayList();
        int pos;
        int max;

        public UndoManager(int maxHistory)
        {
            this.stack = new ArrayList();
            this.max = maxHistory;
        }

        public bool CanUndo
        {
            get
            {
                return this.pos > 0;
            }
        }

        public bool CanRedo
        {
            get
            {
                return this.pos + 1 < this.stack.Count;
            }
        }

        public Command Current
        {
            get
            {
                if (this.pos >= 0 && this.pos < this.stack.Count)
                {
                    return (Command)this.stack[this.pos];
                }
                return null;
            }
        }

        public Command Undo()
        {
            if (this.pos > 0)
            {
                this.pos--;
                return this.Current;
            }
            return null;
        }

        public Command Redo()
        {
            if (this.pos + 1 < this.stack.Count)
            {
                this.pos++;
                Command cmd = this.Current;
                if (cmd != null)
                {
                    cmd.Redo();
                }
                return cmd;
            }
            return null;
        }

        public void Push(Command cmd)
        {
            cmd.Done();

            if (this.pos < this.stack.Count)
            {
                this.stack.RemoveRange(this.pos, this.stack.Count - this.pos);
            }
            this.stack.Add(cmd);
            if (this.stack.Count > this.max)
            {
                this.stack.RemoveAt(0); // bye bye old guy
            }
            else
            {
                this.pos++;
            }
        }

        // Remove top most command (but don't call Undo on it)
        public Command Pop()
        {
            if (this.pos == this.stack.Count && this.stack.Count > 0)
            {
                this.pos--;
                Command cmd = this.Current;
                this.stack.RemoveAt(this.pos);
                return cmd;
            }
            return null;
        }

        public int Count { get { return this.stack.Count; } }

        public object this[int i]
        {
            get { return this.stack[i]; }
        }
    }

    public abstract class Command
    {

        public abstract void Done();
        public abstract void Undo();
        public abstract void Redo();

    }
}

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
                return pos > 0;
            }
        }

        public bool CanRedo
        {
            get
            {
                return pos + 1 < stack.Count;
            }
        }

        public Command Current
        {
            get
            {
                if (pos >= 0 && pos < stack.Count)
                {
                    return (Command)stack[pos];
                }
                return null;
            }
        }

        public Command Undo()
        {
            if (pos > 0)
            {
                pos--;
                Command cmd = this.Current;
                if (cmd != null)
                {
                    cmd.Undo();
                }
                return cmd;
            }
            return null;
        }

        public Command Redo()
        {
            if (pos + 1 < stack.Count)
            {
                pos++;
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

            if (pos < stack.Count)
            {
                stack.RemoveRange(pos, stack.Count - pos);
            }
            stack.Add(cmd);
            if (stack.Count > this.max)
            {
                stack.RemoveAt(0); // bye bye old guy
            }
            else
            {
                pos++;
            }
        }

        // Remove top most command (but don't call Undo on it)
        public Command Pop()
        {
            if (pos == stack.Count && stack.Count > 0)
            {
                pos--;
                Command cmd = this.Current;
                stack.RemoveAt(pos);
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

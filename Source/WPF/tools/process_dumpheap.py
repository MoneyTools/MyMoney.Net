import argparse
import re
import os

# This script can be used to compare the heap before and after a test scenario
# subtracting a baseline so the leaking object are easier to see.
# This uses https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-sos
# which is the https://learn.microsoft.com/en-us/dotnet/core/diagnostics/sos-debugging-extension
# that can be added to windbg which you can get from:
# https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/debugger-download-tools
#
# When windbg starts before you us 'g' run this command:
# .load C:\Users\lovet\.dotnet\sos\sos.dll
#
# And then the dumpheap command becomes available, as well as gcroot and many other handy tools.
class HeapInfo:
    def __init__(self, mt, count, total_size, class_name):
        self.mt = mt
        self.count = count
        self.total_size = total_size
        self.class_name = class_name

    @staticmethod
    def parse(line):
        # 00007ff80d35b350        1           24 System.Threading.Tasks.SynchronizationContextAwaitTaskContinuation+<>c
        ws = re.compile('\s+')
        mt = None
        count = None
        total_size = None
        class_name = None
        try:
            if len(line) > 40:
                class_name = line[39:].strip()
                cols = line[0:39].strip()
                numbers = ws.sub(' ', cols).split(' ')
                if len(numbers) == 3:
                    mt, count, total_size = numbers
                    count = int(count)
                    total_size = int(total_size)
                else:
                    return None
            else:
                return None
        except Exception as e:
            return None
        return HeapInfo(mt, count, total_size, class_name)


def parse_data(filename):
    heap = {}
    with open(filename, 'r') as f:
        for line in f.readlines():
            i = HeapInfo.parse(line)
            if i is not None:
                heap[i.mt] = i

    return heap


def subtract(baseline, heap):
    for k in baseline.keys():
        a = baseline[k]
        if k in heap:
            b = heap[k]
            b.count -= a.count
            b.total_size -= a.total_size


def write_csv(csvfile, heap):
    # convert to a sorted list.
    data = [heap[i] for i in heap.keys() if heap[i].count > 0]
    data.sort(key=lambda h: h.count, reverse=True)
    with open(csvfile, 'w')  as f:
        f.write('count,total_size,mt,class_name\n')
        for h in data:
            f.write(f'{h.count},{h.total_size},{h.mt},"{h.class_name}"\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        description='Read 3 sos dumpheap logs for comparison, first is a baseline, ' +
        'second is before scenario and third is an after scenario. ' +
        'It produces 2 csv files which is before - baseline and after - baseline' )
    parser.add_argument('baseline', help='Text file containing the log output.')
    parser.add_argument('before', help='Text file containing the log output.')
    parser.add_argument('after', help='Text file containing the log output.')

    args = parser.parse_args()
    baseline = parse_data(args.baseline)
    before = parse_data(args.before)
    after = parse_data(args.after)

    subtract(baseline, before)
    subtract(baseline, after)

    # write before.csv
    dir = os.path.dirname(os.path.realpath(args.before))
    csvfile = os.path.splitext(os.path.basename(args.before))[0] + '.csv'
    csvfile = os.path.join(dir, csvfile)
    write_csv(csvfile, before)

    # write after.csv
    csvfile = os.path.splitext(os.path.basename(args.after))[0] + '.csv'
    csvfile = os.path.join(dir, csvfile)
    write_csv(csvfile, after)
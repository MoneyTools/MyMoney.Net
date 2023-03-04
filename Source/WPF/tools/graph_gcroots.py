import argparse
import os


def escape_xml(label):
    return label.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;')


def unmangle(name):
    arrays = ''
    while name.endswith('[]'):
        arrays += '[]'
        name = name[:-2]

    if '`' in name:
        i = name.find('`')
        prefix = name[0:i]
        j = name.find('[', i)
        if j > i:
            tail = name[j:].replace('[','<').replace(']','>')
            return prefix + tail + arrays
    return name + arrays


def is_hex(s):
    try:
        int(s, 16)
        return True
    except ValueError:
        return False


class Node:
    def __init__(self, id, label, category=None):
        self.id = id
        self.label = label
        self.category = category


class Link:
    def __init__(self, source, target, label = None, category=None):
        self.source = source
        self.target = target
        self.label = label
        self.category = category


class Graph:
    def __init__(self):
        self.nodes = {}
        self.links = {}

    def get_or_create_node(self, id, label=None, category=None):
        if id not in self.nodes:
            self.nodes[id] = Node(id, label, category)
        return self.nodes[id]

    def get_or_create_link(self, source, target, label=None, category=None):
        id = f'{source.id}->{target.id}'
        if id not in self.links:
            self.links[id] = Link(source, target, label, category)
        return self.links[id]

    def write(self, filename):
        with open(filename, 'w') as f:
            f.write('<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">\n')
            f.write('<Nodes>\n')
            for k in self.nodes.keys():
                n = self.nodes[k]
                f.write(f'  <Node Id="{escape_xml(n.id)}" Label="{escape_xml(n.label)}"/>\n')
            f.write('</Nodes>\n')
            f.write('<Links>\n')
            for k in self.links.keys():
                link = self.links[k]
                label = ''
                if link.label:
                    label = f'Label="{escape_xml(n.Label)}"'
                f.write(f'  <Link Source="{escape_xml(link.source.id)}" Target="{escape_xml(link.target.id)}" {label}/>\n')
            f.write('</Links>\n')
            f.write('</DirectedGraph>\n')


def parse_graph(filename):
    graph = Graph()
    previous = None
    with open(filename, 'r') as f:
        for line in f.readlines():
            line = line.strip()
            if line.startswith('->'):
                # ->  000001DE8B4520D0 System.Windows.Threading.Dispatcher
                line = line[2:].strip()
                i = line.find(' ')
                if i == 16:
                    address = line[0:i]
                    if is_hex(address):
                        name = unmangle(line[i + 1:])
                        node = graph.get_or_create_node(address, name)
                        if previous is not None:
                            graph.get_or_create_link(previous, node)
                        previous = node
    return graph


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Convert gcroot log to a DGML graph')
    parser.add_argument('path', help='Text file containing the gcroot log output.')
    args = parser.parse_args()

    path = args.path
    graph = parse_graph(path)

    dir = os.path.dirname(os.path.realpath(path))
    dgml_file = os.path.splitext(os.path.basename(path))[0] + '.dgml'
    dgml_file = os.path.join(dir, dgml_file)
    graph.write(dgml_file)
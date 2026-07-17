"""
FindMissingRanges.py

Searches all XML files in D:\Finances\MyMoney\StockQuotes\
and finds those with non-empty MissingRanges elements.
"""
import argparse
import os
import xml.etree.ElementTree as ET
from pathlib import Path


def find_missing_ranges(search_dir: str) -> None:
    """
    Search for XML files with non-empty MissingRanges elements.

    Args:
        search_dir: Directory to search for XML files
    """

    if not os.path.exists(search_dir):
        print(f"Error: Directory does not exist: {search_dir}")
        return

    found_count = 0
    file_count = 0

    print(f"Searching for XML files in: {search_dir}\n")

    # Walk through directory tree
    for root, dirs, files in os.walk(search_dir):
        for file in files:
            if file.lower().endswith('.xml'):
                file_count += 1
                file_path = os.path.join(root, file)

                try:
                    tree = ET.parse(file_path)
                    root_element = tree.getroot()

                    # Find all MissingRanges elements
                    first = True
                    for element in root_element.iter('MissingRanges'):
                        for range in element.iter("DateRange"):
                            if first:
                                first = False;
                                print(f"✓ Found non-empty MissingRanges in: {file_path}")
                            start = range.find("Start")
                            end = range.find("End")
                            print(f"  {start.text} to {end.text}")
                            found_count += 1

                except ET.ParseError as e:
                    print(f"✗ Error parsing {file_path}: {e}")
                except Exception as e:
                    print(f"✗ Error processing {file_path}: {e}")

    print(f"\n{'='*60}")
    print(f"Summary: Processed {file_count} XML files")
    print(f"Found {found_count} files with non-empty MissingRanges elements")
    print(f"{'='*60}")


if __name__ == "__main__":
    # parse command line args to get search_dir
    parser = argparse.ArgumentParser(description="Search for XML files with non-empty MissingRanges elements.")
    parser.add_argument("--search_dir", "-s", type=str,
                        help="Directory to search for XML files (default: D:\\Finances\\MyMoney\\StockQuotes)")
    args = parser.parse_args()
    find_missing_ranges(args.search_dir)

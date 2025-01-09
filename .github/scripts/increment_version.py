#!/usr/bin/env python3

import os
import sys
import xml.etree.ElementTree as ET
import subprocess

def fail(msg: str):
    print(f"Error: {msg}")
    sys.exit(1)

def main():
    # 1) Check if Directory.Build.props exists
    if not os.path.isfile("Directory.Build.props"):
        fail("Directory.Build.props not found.")

    # 2) Parse the XML
    try:
        tree = ET.parse("Directory.Build.props")
    except Exception as ex:
        fail(f"Could not parse XML from Directory.Build.props: {ex}")

    root = tree.getroot()
    property_group = root.find("PropertyGroup")
    if property_group is None:
        fail("No <PropertyGroup> found in Directory.Build.props.")

    # Helper to find a text value or fail
    def get_node_text(tag_name):
        node = property_group.find(tag_name)
        if node is None or not node.text:
            fail(f"<{tag_name}> not found in Directory.Build.props.")
        return node.text.strip()

    major_str = get_node_text("MajorVersion")
    minor_str = get_node_text("MinorVersion")
    patch_str = get_node_text("PatchVersion")

    # 3) Convert to integers
    try:
        current_major = int(major_str)
        current_minor = int(minor_str)
        current_patch = int(patch_str)
    except ValueError:
        fail("Major/Minor/Patch elements must be valid integers.")

    # 4) Increment based on environment variable RELEASE_TYPE
    release_type = os.environ.get("RELEASE_TYPE", "patch")
    if release_type == "patch":
        current_patch += 1
    elif release_type == "minor":
        current_minor += 1
        current_patch = 0
    elif release_type == "major":
        current_major += 1
        current_minor = 0
        current_patch = 0

    new_version = f"{current_major}.{current_minor}.{current_patch}"

    # 5) Print outputs for subsequent steps
    print(f"::set-output name=fullsemver::{new_version}")
    print(f"::set-output name=major::{current_major}")
    print(f"::set-output name=minor::{current_minor}")
    print(f"::set-output name=patch::{current_patch}")

    # 6) For tag comparison, pick up the latest annotated tag if it exists
    try:
        previous_tag = subprocess.check_output(
            ["git", "describe", "--tags", "--abbrev=0"],
            stderr=subprocess.DEVNULL
        ).decode().strip()
    except subprocess.CalledProcessError:
        previous_tag = ""
    print(f"::set-output name=previous_tag::{previous_tag}")

if __name__ == "__main__":
    main()
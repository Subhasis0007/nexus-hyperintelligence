from setuptools import setup, find_packages

setup(
    name="nexus-hyperintelligence-sdk",
    version="0.1.0",
    description="Python SDK for the Nexus HyperIntelligence platform",
    long_description=open("README.md", encoding="utf-8").read() if __import__("os").path.exists("README.md") else "",
    long_description_content_type="text/markdown",
    author="Nexus HyperIntelligence",
    python_requires=">=3.9",
    packages=find_packages(),
    classifiers=[
        "Programming Language :: Python :: 3",
        "License :: OSI Approved :: MIT License",
        "Operating System :: OS Independent",
    ],
)

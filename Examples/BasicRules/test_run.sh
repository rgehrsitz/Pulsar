#!/bin/bash
set -e

echo "=== Testing Pulsar Commands ==="
echo ""

# Clean output directory - output is excluded from version control
rm -rf output
mkdir -p output

echo "Note: Generated output will be placed in the ./output directory, which is excluded from version control."
echo ""

echo "=== Testing Test Command ==="
echo "Running test command with system config..."
dotnet run --project ../../Pulsar.Compiler test \
  --rules=./temperature_rules.yaml \
  --config=./system_config.yaml \
  --output=./output/test

if [ $? -eq 0 ]; then
  echo "✅ Test command passed!"
else
  echo "❌ Test command failed!"
fi

echo ""
echo "=== Testing Beacon Generation ==="
echo "Running with simplified reference config..."
dotnet run --project ../../Pulsar.Compiler beacon \
  --rules=./temperature_rules.yaml \
  --config=./reference_config.yaml \
  --output=./output/reference

if [ $? -eq 0 ]; then
  echo "✅ Reference config test passed!"
else
  echo "❌ Reference config test failed!"
fi

echo ""
echo "Running with full system config..."
dotnet run --project ../../Pulsar.Compiler beacon \
  --rules=./temperature_rules.yaml \
  --config=./system_config.yaml \
  --output=./output/full

if [ $? -eq 0 ]; then
  echo "✅ Full system config test passed!"
else
  echo "❌ Full system config test failed!"
fi

echo ""
echo "Done!"

import os

parts = [
    r'd:/Project/Vue js/FE_NewSales/customer_part1.tmp',
    r'd:/Project/Vue js/FE_NewSales/customer_part2.tmp',
    r'd:/Project/Vue js/FE_NewSales/customer_part3.tmp'
]
target = r'd:/Project/Vue js/FE_NewSales/src/pages/customer-list.vue'

try:
    with open(target, 'wb') as outfile:
        for fname in parts:
            if os.path.exists(fname):
                with open(fname, 'rb') as infile:
                    outfile.write(infile.read())
                print(f"Appended {fname}")
            else:
                print(f"File not found: {fname}")

    print("Concatenation complete.")
    
    # Cleanup
    for fname in parts:
        if os.path.exists(fname):
            os.remove(fname)
    print("Cleanup complete.")

except Exception as e:
    print(f"Error: {e}")

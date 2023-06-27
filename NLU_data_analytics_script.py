import csv
from openpyxl import Workbook
from collections import defaultdict

def get_column_number(column_name):
    column_number = int(input(f"Please enter the column number for {column_name}: ")) - 1
    return column_number

def read_csv(file_name):
    with open(file_name, newline='') as csvfile:
        reader = csv.reader(csvfile)
        data = [row for row in reader]
    return data

def reorganize_logs(data, unique_caller_col, caller_said_col, bot_responded_col, bot_handled_col, intent_col, inter_intent_col):
    organized_data = defaultdict(list)
    for row in data[1:]:
        if unique_caller_col < len(row) and caller_said_col < len(row) and bot_responded_col < len(row) and bot_handled_col < len(row) and intent_col < len(row) and inter_intent_col < len(row):
            unique_caller = row[unique_caller_col]
            caller_said = row[caller_said_col]
            bot_responded = row[bot_responded_col]
            bot_handled = row[bot_handled_col]
            intent = row[intent_col]
            inter_intent = row[inter_intent_col]
            organized_data[unique_caller].append([caller_said, bot_responded, bot_handled, intent, inter_intent])
    return organized_data

def calculate_score(interactions):
    score = 0
    for interaction in interactions:
        if interaction[3] in ["FallbackIntent", "Associate"]:
            score -= 1
    return score

def write_customer_journey(organized_data, wb):
    ws = wb.create_sheet("Customer Journey")

    headers = ["Group ID", "Intent", "InterIntent", "Unique Caller", "What the Caller Said", "How the Bot Responded", "How the Bot Handled the Request", "Intent History", "InterIntent History", "Score"]
    ws.append(headers)

    groups = defaultdict(list)
    group_id = 1

    for caller, interactions in organized_data.items():
        if not caller:
            continue  # Skip if the caller is empty

        key = tuple((interaction[3], interaction[4]) for interaction in interactions)
        if key not in groups:
            groups[key] = group_id
            group_id += 1

        intent_history = []
        inter_intent_history = []
        score = calculate_score(interactions)
        for interaction in interactions:
            intent_history.append(interaction[3])
            inter_intent_history.append(interaction[4])
            ws.append([groups[key], interaction[3], interaction[4], caller] + interaction[:3] + [', '.join(intent_history), ', '.join(inter_intent_history), score])

def write_to_workbook(organized_data, output_file):
    wb = Workbook()
    ws = wb.active
    ws.title = "Reorganized Logs"

    headers = ["Unique Caller", "What the Caller Said", "How the Bot Responded", "How the Bot Handled the Request", "Intent", "InterIntent"]
    ws.append(headers)

    for caller, interactions in organized_data.items():
        for interaction in interactions:
            ws.append([caller] + interaction)

    write_customer_journey(organized_data, wb)

    wb.save(output_file)

def main():
    input_file = input("Please enter the .csv file name: ")
    unique_caller_col = get_column_number("Unique Caller")
    caller_said_col = get_column_number("What the Caller Said")
    bot_responded_col = get_column_number("How the Bot Responded")
    bot_handled_col = get_column_number("How the Bot Handled the Request")
    intent_col = get_column_number("Intent")
    inter_intent_col = get_column_number("InterIntent")

    data = read_csv(input_file)
    organized_data = reorganize_logs(data, unique_caller_col, caller_said_col, bot_responded_col, bot_handled_col, intent_col, inter_intent_col)

    output_file = input("Please enter the output workbook file name (including .xlsx extension): ")
    write_to_workbook(organized_data, output_file)

if __name__ == "__main__":
    main()

from os import getenv
from typing import Optional

import requests


def get_github_prs(token: str, owner: str, repo: str, label: str = "", state: str = "all") -> list[dict]:
    """
    Fetches pull requests from a GitHub repository that match a given label and state.

    Args:
        token (str): GitHub token.
        owner (str): The owner of the repository.
        repo (str): The name of the repository.
        label (str): The label name. Filter is not applied when empty string.
        state (str): State of PR, e.g. open, closed, all

    Returns:
        list: A list of dictionaries, where each dictionary represents a pull request.
              Returns an empty list if no PRs are found or an error occurs.
    """
    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json",
    }

    # This endpoint allows filtering by label(and milestone). A PR in GH's perspective is a type of issue.
    prs_url = f"https://api.github.com/repos/{owner}/{repo}/issues"
    params = {
        "state": state,
        "labels": label,
        "per_page": 100,
    }

    all_prs = []
    page = 1
    while True:
        try:
            params["page"] = page
            response = requests.get(prs_url, headers=headers, params=params)
            response.raise_for_status()  # Raise an exception for HTTP errors
            prs = response.json()

            if not prs:
                break  # No more PRs to fetch

            # Check for pr key since we are using issues endpoint instead.
            all_prs.extend([item for item in prs if "pull_request" in item])
            page += 1

        except requests.exceptions.RequestException as e:
            print(f"Error fetching pull requests: {e}")
            exit(1)

    return all_prs


def get_prs(
    pull_request_items: list[dict], label: str = "", state: str = "all", milestone_number: Optional[int] = None
) -> list[dict]:
    """
    Returns a list of pull requests after applying the label and state filters.

    Args:
        pull_request_items (list[dict]): List of PR items.
        label (str): The label name. Filter is not applied when empty string.
        state (str): State of PR, e.g. open, closed, all
        milestone_number (Optional[int]): The milestone number to filter by. If None, no milestone filtering is applied.

    Returns:
        list: A list of dictionaries, where each dictionary represents a pull request.
              Returns an empty list if no PRs are found.
    """
    pr_list = []
    count = 0
    for pr in pull_request_items:
        if state not in [pr["state"], "all"]:
            continue

        if label and not [item for item in pr["labels"] if item["name"] == label]:
            continue

        if milestone_number:
            if not pr.get("milestone") or pr["milestone"]["number"] != milestone_number:
                continue

        pr_list.append(pr)
        count += 1

    print(
        f"Found {count} PRs with {label if label else 'no filter on'} label, state as {state}, and milestone {pr.get('milestone', {}).get('number', 'None')}"
    )

    return pr_list


def get_prs_assignees(pull_request_items: list[dict]) -> list[str]:
    """
    Returns a list of pull request assignees, excludes jjw24.

    Args:
        pull_request_items (list[dict]): List of PR items to get the assignees from.

    Returns:
        list: A list of strs, where each string is an assignee name. List is not distinct, so can contain
              duplicate names.
              Returns an empty list if none are found.
    """
    assignee_list = []
    for pr in pull_request_items:
        [assignee_list.append(assignee["login"]) for assignee in pr["assignees"] if assignee["login"] != "jjw24"]

    print(f"Found {len(assignee_list)} assignees")

    return assignee_list


def get_pr_descriptions(pull_request_items: list[dict]) -> str:
    """
    Returns the concatenated string of pr title and number in the format of
    '- PR title 1 #3651
     - PR title 2 #3652
     - PR title 3 #3653
    '

    Args:
        pull_request_items (list[dict]): List of PR items.

    Returns:
        str: a string of PR titles and numbers
    """
    description_content = ""
    for pr in pull_request_items:
        description_content += f"- {pr['title']} #{pr['number']}\n"

    return description_content


def update_pull_request_description(token: str, owner: str, repo: str, pr_number: int, new_description: str) -> None:
    """
    Updates the description (body) of a GitHub Pull Request.

    Args:
        token (str): Token.
        owner (str): The owner of the repository.
        repo (str): The name of the repository.
        pr_number (int): The number of the pull request to update.
        new_description (str): The new content for the PR's description.

    Returns:
        dict or None: The updated PR object (as a dictionary) if successful,
                      None otherwise.
    """
    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json",
        "Content-Type": "application/json",
    }

    url = f"https://api.github.com/repos/{owner}/{repo}/pulls/{pr_number}"

    payload = {"body": new_description}

    print(f"Attempting to update PR #{pr_number} in {owner}/{repo}...")
    print(f"URL: {url}")

    try:
        response = None
        response = requests.patch(url, headers=headers, json=payload)
        response.raise_for_status()

        print(f"Successfully updated PR #{pr_number}.")

    except requests.exceptions.RequestException as e:
        print(f"Error updating pull request #{pr_number}: {e}")
        if response is not None:
            print(f"Response status code: {response.status_code}")
            print(f"Response text: {response.text}")
        exit(1)


if __name__ == "__main__":
    github_token = getenv("GITHUB_TOKEN")

    if not github_token:
        print("Error: GITHUB_TOKEN environment variable not set.")
        exit(1)

    repository_owner = "flow-launcher"
    repository_name = "flow.launcher"
    state = "all"

    print(f"Fetching {state} PRs for {repository_owner}/{repository_name} ...")

    # First, get all PRs to find the release PR and determine the milestone
    all_pull_requests = get_github_prs(github_token, repository_owner, repository_name)

    if not all_pull_requests:
        print("No pull requests found")
        exit(1)

    print(f"\nFound total of {len(all_pull_requests)} pull requests")

    release_pr = get_prs(all_pull_requests, "release", "open")

    if len(release_pr) != 1:
        print(f"Unable to find the exact release PR. Returned result: {release_pr}")
        exit(1)

    print(f"Found release PR: {release_pr[0]['title']}")

    release_milestone_number = release_pr[0].get("milestone", {}).get("number", None)

    if not release_milestone_number:
        print("Release PR does not have a milestone assigned.")
        exit(1)

    print(f"Using milestone number: {release_milestone_number}")

    enhancement_prs = get_prs(all_pull_requests, "enhancement", "closed", release_milestone_number)
    bug_fix_prs = get_prs(all_pull_requests, "bug", "closed", release_milestone_number)

    description_content = "# Release notes\n"
    description_content += f"## Features\n{get_pr_descriptions(enhancement_prs)}" if enhancement_prs else ""
    description_content += f"## Bug fixes\n{get_pr_descriptions(bug_fix_prs)}" if bug_fix_prs else ""

    assignees = list(set(get_prs_assignees(enhancement_prs) + get_prs_assignees(bug_fix_prs)))
    assignees.sort(key=str.lower)

    description_content += f"### Authors:\n{', '.join(assignees)}"

    update_pull_request_description(
        github_token, repository_owner, repository_name, release_pr[0]["number"], description_content
    )

    print(f"PR content updated to:\n{description_content}")

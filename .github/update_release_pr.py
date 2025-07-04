from os import getenv

import requests


def get_github_prs(token: str, owner: str, repo: str, label: str = "", state: str = "all") -> list[dict]:
    """
    Fetches pull requests from a GitHub repository that match a given milestone and label.

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

    milestone_id = None
    milestone_url = f"https://api.github.com/repos/{owner}/{repo}/milestones"
    params = {"state": "open"}

    try:
        response = requests.get(milestone_url, headers=headers, params=params)
        response.raise_for_status()
        milestones = response.json()

        if len(milestones) > 2:
            print("More than two milestones found, unable to determine the milestone required.")
            exit(1)

        # milestones.pop()
        for ms in milestones:
            if ms["title"] != "Future":
                milestone_id = ms["number"]
                print(f"Gathering PRs with milestone {ms['title']}...")
                break

        if not milestone_id:
            print(f"No suitable milestone found in repository '{owner}/{repo}'.")
            exit(1)

    except requests.exceptions.RequestException as e:
        print(f"Error fetching milestones: {e}")
        exit(1)

    # This endpoint allows filtering by milestone and label. A PR in GH's perspective is a type of issue.
    prs_url = f"https://api.github.com/repos/{owner}/{repo}/issues"
    params = {
        "state": state,
        "milestone": milestone_id,
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


def get_prs(pull_request_items: list[dict], label: str = "", state: str = "all") -> list[dict]:
    """
    Returns a list of pull requests after applying the label and state filters.

    Args:
        pull_request_items (list[dict]): List of PR items.
        label (str): The label name. Filter is not applied when empty string.
        state (str): State of PR, e.g. open, closed, all

    Returns:
        list: A list of dictionaries, where each dictionary represents a pull request.
              Returns an empty list if no PRs are found.
    """
    pr_list = []
    count = 0
    for pr in pull_request_items:
        if state in [pr["state"], "all"] and (not label or [item for item in pr["labels"] if item["name"] == label]):
            pr_list.append(pr)
            count += 1

    print(f"Found {count} PRs with {label if label else 'no filter on'} label and state as {state}")

    return pr_list

def get_prs_assignees(pull_request_items: list[dict], label: str = "", state: str = "all") -> list[str]:
    """
    Returns a list of pull request assignees after applying the label and state filters, excludes jjw24.

    Args:
        pull_request_items (list[dict]): List of PR items.
        label (str): The label name. Filter is not applied when empty string.
        state (str): State of PR, e.g. open, closed, all

    Returns:
        list: A list of strs, where each string is an assignee name. List is not distinct, so can contain
              duplicate names.
              Returns an empty list if none are found.
    """
    assignee_list = []
    for pr in pull_request_items:
        if state in [pr["state"], "all"] and (not label or [item for item in pr["labels"] if item["name"] == label]):
            [assignee_list.append(assignee["login"]) for assignee in pr["assignees"] if assignee["login"] != "jjw24" ]

    print(f"Found {len(assignee_list)} assignees with {label if label else 'no filter on'} label and state as {state}")

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

    pull_requests = get_github_prs(github_token, repository_owner, repository_name)

    if not pull_requests:
        print("No matching pull requests found")
        exit(1)

    print(f"\nFound total of {len(pull_requests)} pull requests")

    release_pr = get_prs(pull_requests, "release", "open")

    if len(release_pr) != 1:
        print(f"Unable to find the exact release PR. Returned result: {release_pr}")
        exit(1)

    print(f"Found release PR: {release_pr[0]['title']}")

    enhancement_prs = get_prs(pull_requests, "enhancement", "closed")
    bug_fix_prs = get_prs(pull_requests, "bug", "closed")

    description_content = "# Release notes\n"
    description_content += f"## Features\n{get_pr_descriptions(enhancement_prs)}" if enhancement_prs else ""
    description_content += f"## Bug fixes\n{get_pr_descriptions(bug_fix_prs)}" if bug_fix_prs else ""

    assignees = list(set(get_prs_assignees(pull_requests, "enhancement", "closed") + get_prs_assignees(pull_requests, "bug", "closed")))
    assignees.sort(key=str.lower)

    description_content += f"### Authors:\n{', '.join(assignees)}"

    update_pull_request_description(
        github_token, repository_owner, repository_name, release_pr[0]["number"], description_content
    )

    print(f"PR content updated to:\n{description_content}")

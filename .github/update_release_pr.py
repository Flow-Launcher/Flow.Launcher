import os
import requests

def get_github_prs(token, owner, repo, label = "", state ="all"):
    """
    Fetches pull requests from a GitHub repository that match a given milestone and label.

    Args:
        token (str): GitHub token.
        owner (str): The owner of the repository.
        repo (str): The name of the repository.
        label (str): The label name.
        state (str): State of PR, e.g. open

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
    params = {"state": open}
    
    try:
        response = requests.get(milestone_url, headers=headers, params=params)
        response.raise_for_status()
        milestones = response.json()

        if len(milestones) > 2:
            print("More than two milestones found, unable to determine the milestone required.")

        # milestones.pop()
        for ms in milestones:
            if ms["title"] != "Future":
                milestone_id = ms["number"]
                print(f"Gathering PRs with milestone {ms['title']}..." )
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
            response.raise_for_status() # Raise an exception for HTTP errors
            prs = response.json()
            
            if not prs:
                break # No more PRs to fetch
            
            # Check for pr key since we are using issues endpoint instead.
            all_prs.extend([item for item in prs if "pull_request" in item])
            page += 1

        except requests.exceptions.RequestException as e:
            print(f"Error fetching pull requests: {e}")
            exit(1)
            
    return all_prs

def get_prs(pull_request_items, label= "", state= "all"):
    pr_list = []
    count = 0
    for pr in pull_request_items:
        if pr["state"] == state and [item for item in pr["labels"] if item["name"] == label]:
            pr_list.append(pr)
            count += 1
    
    print(f"Found {count} PRs with {label if label else "no"} label and state as {state}")

    return pr_list

def get_pr_descriptions(pull_request_items):
    description_content = ""
    for pr in pull_request_items:
        description_content+= f"- {pr['title']} #{pr['number']}\n"

    return description_content

def update_pull_request_description(token, owner, repo, pr_number, new_description):
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
        "Content-Type": "application/json"
    }

    url = f"https://api.github.com/repos/{owner}/{repo}/pulls/{pr_number}"

    payload = {
        "body": new_description
    }

    print(f"Attempting to update PR #{pr_number} in {owner}/{repo}...")
    print(f"URL: {url}")

    try:
        response = requests.patch(url, headers=headers, json=payload)
        response.raise_for_status()

        updated_pr_data = response.json()
        print(f"Successfully updated PR #{pr_number}.")
        return updated_pr_data

    except requests.exceptions.RequestException as e:
        print(f"Error updating pull request #{pr_number}: {e}")
        if response is not None:
            print(f"Response status code: {response.status_code}")
            print(f"Response text: {response.text}")
        return None


if __name__ == "__main__":
    github_token = os.environ.get("GITHUB_TOKEN")
    
    if not github_token:
        print("Error: GITHUB_TOKEN environment variable not set.")
        exit(1)

    repository_owner = "flow-launcher"
    repository_name = "flow.launcher"
    target_label = "enhancement"
    state = "all"

    print(f"Fetching PRs for {repository_owner}/{repository_name} with label '{target_label}'...")

    pull_requests = get_github_prs(
        github_token, 
        repository_owner, 
        repository_name
    )

    if not pull_requests:
        print("No matching pull requests found")
        exit(1)

    print(f"\nFound total of {len(pull_requests)} pull requests")

    release_pr = get_prs(
        pull_requests,
        "release",
        "open"
    )

    if len(release_pr) != 1:
        print(f"Unable to find the exact release PR. Returned result: {release_pr}")
        exit(1)
    
    print(f"Found release PR: {release_pr[0]['title']}")

    enhancement_prs = get_prs(pull_requests, "enhancement", "closed")
    bug_fix_prs = get_prs(pull_requests, "bug", "closed")

    description_content = "# Release notes\n"
    description_content += f"## Features\n{get_pr_descriptions(enhancement_prs)}" if enhancement_prs else ""
    description_content += f"## Bug fixes\n{get_pr_descriptions(bug_fix_prs)}" if bug_fix_prs else ""

    update_pull_request_description(github_token, repository_owner, repository_name, release_pr[0]["number"], description_content)

    print(f"PR content updated to:\n{description_content}")
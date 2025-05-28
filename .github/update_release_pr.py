import os
import requests

def get_github_prs(token, owner, repo, label, state):
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

if __name__ == "__main__":
    github_token = os.environ.get("GITHUB_TOKEN")
    
    if not github_token:
        print("Error: GITHUB_TOKEN environment variable not set.")
        exit(1)

    repository_owner = "flow-launcher"
    repository_name = "flow.launcher"
    target_label = "enhancement"
    state = "closed"

    print(f"Fetching PRs for {repository_owner}/{repository_name} with label '{target_label}'...")
    
    pull_requests = get_github_prs(
        github_token, 
        repository_owner, 
        repository_name, 
        target_label,
        state
    )

    if pull_requests:
        print(f"\nFound {len(pull_requests)} pull requests:")
        for pr in pull_requests:
            print(f"- {pr['state']} #{pr['number']}: {pr['title']} (URL: {pr['html_url']})")
    else:
        print("No matching pull requests found.")
